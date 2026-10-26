using Ardan.Core.Fleet;
using Ardan.Core.Layout;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Core.Engine;

public sealed partial class Warehouse
{
    private void AssignJobs()
    {
        var waiting = _jobs
            .Where(job => job.Status == JobStatus.Waiting)
            .OrderByDescending(job => job.Priority)
            .ThenBy(job => job.CreatedAt)
            .ToList();

        foreach (var job in waiting)
        {
            var taxi = _taxis.Where(t => t.CanTakeJob).MinBy(t => SecondsToReach(t, job.From));
            if (taxi is null) return;
            Assign(taxi, job);
        }
    }

    private void Assign(Taxi taxi, Job job)
    {
        job.Status = JobStatus.Assigned;
        job.TaxiId = taxi.Id;
        job.AssignedAt = Now;
        taxi.Job = job;
        taxi.Route.Clear();
        Enqueue(taxi, PathBetween(taxi.Level, taxi.X, job.From));
        taxi.Route.Enqueue(new Transfer(Pickup: true, HandlingSeconds(job.From, pickup: true)));
        Enqueue(taxi, PathBetween(job.From.Level, job.From.X, job.To));
        taxi.Route.Enqueue(new Transfer(Pickup: false, HandlingSeconds(job.To, pickup: false)));
    }

    private static void Enqueue(Taxi taxi, IEnumerable<Leg> legs)
    {
        foreach (var leg in legs) taxi.Route.Enqueue(leg);
    }

    private static IEnumerable<Leg> PathBetween(int fromLevel, double fromX, Place to)
    {
        if (fromLevel == to.Level)
        {
            if (Math.Abs(fromX - to.X) > 1e-9) yield return new Drive(to.X);
            yield break;
        }

        if (fromX > 0) yield return new Drive(0);
        yield return new Ride(fromLevel, to.Level);
        if (to.X > 0) yield return new Drive(to.X);
    }

    private double HandlingSeconds(Place place, bool pickup)
    {
        if (place.Kind != PlaceKind.Lane) return Motion.TransferSeconds;
        var lane = Grid[place.Lane];
        var reach = pickup ? lane.FrontReach : lane.DropReach;
        return Motion.TransferSeconds + 2 * reach * Layout.PalletPitchMetres / Motion.TaxiLoadedSpeed;
    }

    private double SecondsToReach(Taxi taxi, Place place)
    {
        var perBay = Layout.BayPitchMetres / Motion.TaxiEmptySpeed;
        if (taxi.Level == place.Level) return Math.Abs(taxi.X - place.X) * perBay;

        var liftQueue = _lifts.Min(lift => lift.Busy) * 12;
        return (taxi.X + place.X) * perBay
            + Math.Abs(taxi.Level - place.Level) * Layout.LevelHeightMetres / Motion.LiftSpeed
            + 2 * Motion.BoardingSeconds
            + liftQueue;
    }

    private void MoveTaxi(Taxi taxi, double seconds)
    {
        if (taxi.FaultCode is not null)
        {
            Drain(taxi, Energy.DrainIdle, seconds);
            return;
        }

        if (taxi.Route.Count == 0)
        {
            Rest(taxi, seconds);
            return;
        }

        switch (taxi.Route.Peek())
        {
            case Drive drive:
                DriveTaxi(taxi, drive, seconds);
                break;
            case Ride ride:
                RequestRide(taxi, ride, seconds);
                break;
            case Transfer transfer:
                TransferPallet(taxi, transfer, seconds);
                break;
        }

        if (taxi.State != TaxiState.Driving) Cool(taxi, seconds);
    }

    private void Rest(Taxi taxi, double seconds)
    {
        if (taxi.X > 0)
        {
            taxi.Route.Enqueue(new Drive(0));
            return;
        }

        _aisles.Leave(taxi.Level, taxi.Id);
        Cool(taxi, seconds);

        if (!taxi.InService)
        {
            taxi.State = TaxiState.OutOfService;
            Drain(taxi, Energy.DrainIdle, seconds);
            return;
        }

        if (taxi.Battery < 99.5)
        {
            taxi.State = TaxiState.Charging;
            taxi.Battery = Math.Min(100, taxi.Battery + Energy.ChargeRate * seconds);
            if (taxi.ChargeHold && taxi.Battery >= Limits.ChargedBattery) taxi.ChargeHold = false;
            return;
        }

        taxi.State = TaxiState.Idle;
        taxi.ChargeHold = false;
    }

    private void DriveTaxi(Taxi taxi, Drive drive, double seconds)
    {
        var inAisle = drive.ToX > 0 || taxi.X > 0;
        if (inAisle && !_aisles.TryEnter(taxi.Level, taxi.Id))
        {
            taxi.State = TaxiState.WaitingForAisle;
            Drain(taxi, Energy.DrainIdle, seconds);
            return;
        }

        taxi.State = TaxiState.Driving;
        var reach = taxi.TravelSpeed * seconds / Layout.BayPitchMetres;
        var gap = drive.ToX - taxi.X;
        var move = Math.Abs(gap) <= reach ? gap : Math.Sign(gap) * reach;
        taxi.X += move;
        taxi.DistanceMetres += Math.Abs(move) * Layout.BayPitchMetres;
        Drain(taxi, Energy.DrainDriving, seconds);
        taxi.MotorTempC = Math.Min(70, taxi.MotorTempC + 0.06 * seconds);

        if (Math.Abs(drive.ToX - taxi.X) > 1e-9) return;
        taxi.X = drive.ToX;
        taxi.Route.Dequeue();
        if (taxi.X == 0) _aisles.Leave(taxi.Level, taxi.Id);
    }

    private void RequestRide(Taxi taxi, Ride ride, double seconds)
    {
        Drain(taxi, Energy.DrainIdle, seconds);
        if (taxi.RideRequested) return;

        var lift = _lifts
            .OrderBy(l => l.FaultCode is null ? 0 : 1)
            .ThenBy(l => l.Busy)
            .ThenBy(l => Math.Abs(l.Position - ride.FromLevel))
            .First();

        lift.Queue.Enqueue(new RideRequest(taxi, ride.FromLevel, ride.ToLevel));
        taxi.RideRequested = true;
        taxi.State = TaxiState.WaitingForLift;
    }

    private void MoveLift(Lift lift, double seconds)
    {
        if (lift.FaultCode is not null) return;

        if (lift.Active is null)
        {
            if (!lift.Queue.TryDequeue(out var next))
            {
                lift.State = LiftState.Idle;
                return;
            }
            lift.Active = next;
            lift.State = LiftState.Moving;
        }

        var ride = lift.Active;
        switch (lift.State)
        {
            case LiftState.Moving:
                if (TravelTo(lift, ride.FromLevel, seconds)) Board(lift, ride.Taxi);
                break;
            case LiftState.Boarding:
                lift.Timer -= seconds;
                if (lift.Timer <= 0) lift.State = LiftState.Carrying;
                break;
            case LiftState.Carrying:
                if (TravelTo(lift, ride.ToLevel, seconds))
                {
                    lift.State = LiftState.Leaving;
                    lift.Timer = Motion.BoardingSeconds;
                }
                break;
            case LiftState.Leaving:
                lift.Timer -= seconds;
                if (lift.Timer <= 0) Unboard(lift, ride);
                break;
        }
    }

    private static void Board(Lift lift, Taxi taxi)
    {
        lift.State = LiftState.Boarding;
        lift.Timer = Motion.BoardingSeconds;
        taxi.Lift = lift;
        taxi.State = TaxiState.OnLift;
    }

    private static void Unboard(Lift lift, RideRequest ride)
    {
        var taxi = ride.Taxi;
        taxi.Level = ride.ToLevel;
        taxi.X = 0;
        taxi.Lift = null;
        taxi.RideRequested = false;
        taxi.Route.Dequeue();
        lift.Active = null;
        lift.State = LiftState.Idle;
    }

    private bool TravelTo(Lift lift, int level, double seconds)
    {
        var reach = Motion.LiftSpeed * seconds / Layout.LevelHeightMetres;
        var gap = level - lift.Position;
        if (Math.Abs(gap) <= reach)
        {
            lift.Position = level;
            return true;
        }
        lift.Position += Math.Sign(gap) * reach;
        return false;
    }

    private void TransferPallet(Taxi taxi, Transfer transfer, double seconds)
    {
        taxi.State = transfer.Pickup ? TaxiState.Loading : TaxiState.Unloading;
        taxi.TransferLeft ??= transfer.Seconds;
        taxi.TransferLeft -= seconds;
        Drain(taxi, Energy.DrainHandling, seconds);
        if (taxi.TransferLeft > 0) return;

        var finished = transfer.Pickup ? PickUp(taxi) : SetDown(taxi);
        if (!finished)
        {
            taxi.TransferLeft = 0.5;
            return;
        }

        taxi.TransferLeft = null;
        taxi.Route.Dequeue();
    }

    private bool PickUp(Taxi taxi)
    {
        var job = taxi.Job!;
        var pallet = job.Pallet;

        if (job.From.Kind == PlaceKind.Infeed)
        {
            var item = Infeed.Items.FirstOrDefault(entry => entry.Pallet == pallet);
            if (item is null) return false;
            Infeed.Remove(item);
        }
        else
        {
            var lane = Grid[job.From.Lane];
            if (lane.Front != pallet) return false;
            lane.Pop();
            lane.Release(job.Id);
            _journal.Moved(new Movement(pallet.Id, Now, MovementKind.Retrieved, lane.Id.ToString(), taxi.Id, taxi.Id));
            Events.Add(Now, EventKind.Retrieved, $"{taxi.Id} picked {pallet.Id} from {lane.Id} for {job.Order?.Id ?? "a transfer"}", pallet.Id);
        }

        pallet.Status = PalletStatus.Moving;
        taxi.Load = pallet;
        _journal.PalletChanged(pallet);
        return true;
    }

    private bool SetDown(Taxi taxi)
    {
        var job = taxi.Job!;
        var pallet = job.Pallet;

        if (job.To.Kind == PlaceKind.Outfeed)
        {
            if (!Outfeed.EntryClear) return false;
            Outfeed.Add(pallet);
        }
        else
        {
            var lane = Grid[job.To.Lane];
            lane.Push(pallet);
            lane.Release(job.Id);
            Throughput.CountIn(Now);
            _journal.PalletChanged(pallet);
            _journal.Moved(new Movement(pallet.Id, Now, MovementKind.Stored, taxi.Id, lane.Id.ToString(), taxi.Id));
            Events.Add(Now, EventKind.Stored, $"{taxi.Id} stored {pallet.Id} in {lane.Id}", pallet.Id);
            if (OrderOf(pallet) is { } order)
            {
                order.Done++;
                CompleteIfDone(order);
            }
        }

        taxi.Load = null;
        FinishJob(taxi, job);
        return true;
    }

    private void FinishJob(Taxi taxi, Job job)
    {
        job.Status = JobStatus.Done;
        job.CompletedAt = Now;
        taxi.Job = null;
        _jobs.Remove(job);

        if (job.Kind != JobKind.Retrieval || job.AssignedAt is null) return;
        _cycleSeconds.Enqueue((Now - job.AssignedAt.Value).TotalSeconds);
        if (_cycleSeconds.Count > 30) _cycleSeconds.Dequeue();
    }

    private static void Drain(Taxi taxi, double ratePerSecond, double seconds) =>
        taxi.Battery = Math.Max(0, taxi.Battery - ratePerSecond * seconds);

    private void Cool(Taxi taxi, double seconds) =>
        taxi.MotorTempC += (Climate.TemperatureC + 6 - taxi.MotorTempC) * Math.Min(1, seconds / 60);
}
