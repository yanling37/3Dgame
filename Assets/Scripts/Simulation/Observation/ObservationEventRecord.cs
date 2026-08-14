using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Immutable copy of a region event at capture time. Not live ActiveEvents.
    /// </summary>
    public sealed class ObservationEventRecord
    {
        public static readonly ObservationEventRecord[] None = new ObservationEventRecord[0];

        public ObservationEventRecord(
            string eventId,
            SimEventType eventType,
            RegionId regionId,
            SimEventScope scope,
            int startDay,
            int duration,
            float severity)
        {
            EventId = eventId ?? string.Empty;
            EventType = eventType;
            RegionId = regionId;
            Scope = scope;
            StartDay = startDay;
            Duration = duration;
            Severity = severity;
        }

        public string EventId { get; }
        public SimEventType EventType { get; }
        public RegionId RegionId { get; }
        public SimEventScope Scope { get; }
        public int StartDay { get; }
        public int Duration { get; }
        public float Severity { get; }
        public int EndDay => StartDay + (Duration < 1 ? 1 : Duration);

        public bool IsTrendMarker
        {
            get
            {
                return EventType == SimEventType.NaturalDisaster
                    || EventType == SimEventType.DiseaseOutbreak
                    || EventType == SimEventType.FoodShortage;
            }
        }

        public bool Overlaps(int rangeStartInclusive, int rangeEndInclusive)
        {
            return StartDay <= rangeEndInclusive && EndDay > rangeStartInclusive;
        }

        public static ObservationEventRecord From(RegionEvent source, RegionId fallbackRegion)
        {
            if (source == null)
            {
                return new ObservationEventRecord(
                    string.Empty,
                    SimEventType.None,
                    fallbackRegion,
                    SimEventScope.Regional,
                    0,
                    1,
                    0f);
            }

            return new ObservationEventRecord(
                source.EventId,
                source.EventType,
                source.RegionId,
                source.Scope,
                source.StartDay,
                source.Duration,
                source.Severity);
        }

        public static string DisplayName(SimEventType type)
        {
            switch (type)
            {
                case SimEventType.NaturalDisaster: return "Natural Disaster";
                case SimEventType.DiseaseOutbreak: return "Disease Outbreak";
                case SimEventType.FoodShortage: return "Food Shortage";
                default: return type.ToString();
            }
        }
    }
}
