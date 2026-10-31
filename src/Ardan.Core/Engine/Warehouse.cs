using Ardan.Core.Alarms;
using Ardan.Core.Fleet;
using Ardan.Core.Layout;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Engine;

/// <summary>Numbers to continue from after a restart, so new ids never clash with old ones.</summary>
public sealed record IdSeeds(
    int Pallet = 100_000,
    int Inbound = 24_100,
    int Outbound = 88_200,
    int Alarm = 0,
    int Asn = 5_500,
    int SalesOrder = 310_400);

/// <summary>The live model of one automated store: rack, fleet, conveyors, orders and alarms.</summary>
public sealed partial class Warehouse
{
    private const double StepSeconds = 0.1;

    private readonly Random _random;
    private readonly IJournal _journal;
    private readonly List<Taxi> _taxis;
    private readonly List<Lift> _lifts;
    private readonly Dictionary<string, Sku> _skus;
    private readonly Dictionary<string, Pallet> _pallets = [];
    private readonly List<Order> _orders = [];
    private readonly List<Job> _jobs = [];
    private readonly AisleTraffic _aisles = new();
    private readonly Queue<double> _cycleSeconds = new();
    private int _palletNumber;
    private int _inboundNumber;
    private int _outboundNumber;
    private int _asnNumber;
    private int _salesOrderNumber;
    private int _jobNumber;
    private double _nextOrderIn = 25;
    private double _nextArrivalIn = 3;

    public Warehouse(
        SiteLayout layout,
        IEnumerable<Sku> skus,
        DateTimeOffset now,
        Random random,
        IJournal journal,
        IdSeeds? seeds = null,
        int taxis = 6)
    {
        seeds ??= new IdSeeds();
        Layout = layout;
        Grid = new StorageGrid(layout);
        Now = now;
        _random = random;
        _journal = journal;
        _skus = skus.ToDictionary(sku => sku.Code);
        _taxis = Enumerable.Range(1, taxis).Select(i => new Taxi($"T-{i:00}", (i - 1) % layout.Levels)).ToList();
        _lifts = [new Lift("V-1", 0), new Lift("V-2", 1) { Position = layout.Levels / 2 }];
        _palletNumber = seeds.Pallet;
        _inboundNumber = seeds.Inbound;
        _outboundNumber = seeds.Outbound;
        _asnNumber = seeds.Asn;
        _salesOrderNumber = seeds.SalesOrder;
        Alarms = new AlarmBoard(journal, seeds.Alarm);
        Score = new ShiftScore(now);
        Alarms.Closed += Score.Closed;
        Throughput.WarmUp(now, random);
    }

    public SiteLayout Layout { get; }
    public StorageGrid Grid { get; }
    public DateTimeOffset Now { get; private set; }
    public double Speed { get; private set; } = 1;
    public bool OrderFeedEnabled { get; set; } = true;
    public bool FaultsEnabled { get; set; } = true;
    public double ProfileFaultRate { get; set; } = 0.025;
    public Conveyor Infeed { get; } = new("C-IN", "Infeed");
    public Conveyor Outfeed { get; } = new("C-OUT", "Outfeed");
    public Climate Climate { get; } = new();
    public AlarmBoard Alarms { get; }
    public EventLog Events { get; } = new();
    public ThroughputMeter Throughput { get; } = new();
    public ShiftScore Score { get; }
    public IReadOnlyList<Taxi> Taxis => _taxis;
    public IReadOnlyList<Lift> Lifts => _lifts;
    public IReadOnlyDictionary<string, Sku> Skus => _skus;
    public IReadOnlyDictionary<string, Pallet> Pallets => _pallets;
    public IReadOnlyList<Order> Orders => _orders;
    public IReadOnlyList<Job> Jobs => _jobs;
    public double AverageCycleSeconds => _cycleSeconds.Count == 0 ? 0 : _cycleSeconds.Average();

    public void Load(IEnumerable<Pallet> storedPallets)
    {
        var ordered = storedPallets
            .Where(pallet => pallet.Lane is not null)
            .OrderBy(pallet => pallet.Lane!.Value.Level)
            .ThenBy(pallet => pallet.Lane!.Value.Bay)
            .ThenBy(pallet => pallet.Slot);

        foreach (var pallet in ordered)
        {
            Grid[pallet.Lane!.Value].Push(pallet);
            _pallets[pallet.Id] = pallet;
            _palletNumber = Math.Max(_palletNumber, NumberIn(pallet.Id));
        }
    }

    public void Load(IEnumerable<Order> openOrders)
    {
        foreach (var order in openOrders)
        {
            _orders.Add(order);
            if (order.Type == OrderType.Inbound) _inboundNumber = Math.Max(_inboundNumber, NumberIn(order.Id));
            else _outboundNumber = Math.Max(_outboundNumber, NumberIn(order.Id));
        }
    }

    public void Advance(TimeSpan elapsed)
    {
        var remaining = elapsed.TotalSeconds * Speed;
        while (remaining > 1e-9)
        {
            var step = Math.Min(remaining, StepSeconds);
            Step(step);
            remaining -= step;
        }
    }

    public Sku SkuOf(string code) => _skus[code];

    public string NameOf(string skuCode) => _skus.TryGetValue(skuCode, out var sku) ? sku.Name : skuCode;

    public Order? FindOrder(string id) =>
        _orders.FirstOrDefault(order => string.Equals(order.Id, id, StringComparison.OrdinalIgnoreCase));

    public Taxi? FindTaxi(string id) =>
        _taxis.FirstOrDefault(taxi => string.Equals(taxi.Id, id, StringComparison.OrdinalIgnoreCase));

    public Lift? FindLift(string id) =>
        _lifts.FirstOrDefault(lift => string.Equals(lift.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Pallets of a product that are in store and not yet promised to an order.</summary>
    public int FreeStock(string skuCode)
    {
        var stored = Grid.StoredOf(skuCode);
        var reserved = _jobs.Count(job =>
            job.Kind == JobKind.Retrieval && job.Pallet.SkuCode == skuCode && job.Pallet.Status == PalletStatus.Stored);
        var promised = _orders
            .Where(order => order.IsOpen && order.Type == OrderType.Outbound && order.SkuCode == skuCode)
            .Sum(order => Math.Max(0, order.StillToStart));
        return Math.Max(0, stored - reserved - promised);
    }

    private void Step(double seconds)
    {
        Now = Now.AddSeconds(seconds);
        FeedOrders(seconds);
        RunInfeed(seconds);
        RunOutfeed(seconds);
        CreateJobs();
        AssignJobs();
        foreach (var lift in _lifts) MoveLift(lift, seconds);
        foreach (var taxi in _taxis) MoveTaxi(taxi, seconds);
        Climate.Advance(seconds, _random);
        if (FaultsEnabled) InjectFaults(seconds);
        WatchConditions(seconds);
        Score.Tick(Now, seconds, AllRunning);
    }

    private bool AllRunning =>
        _taxis.All(taxi => taxi.FaultCode is null) && _lifts.All(lift => lift.FaultCode is null) && Outfeed.FaultCode is null;

    private Order? OrderOf(Pallet pallet) => pallet.OrderId is null ? null : FindOrder(pallet.OrderId);

    private string NextPalletId() => $"PLT-{++_palletNumber}";

    private static int NumberIn(string id) =>
        int.TryParse(id.AsSpan(id.LastIndexOf('-') + 1), out var number) ? number : 0;

    private string NextJobId() => $"J-{++_jobNumber:00000}";
}
