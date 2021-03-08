using System.Collections.Generic;

namespace Eventuous.EventStoreDB.Subscriptions {
    public class ProjectionGapMeasure {
        readonly Dictionary<string, ulong> _gaps = new();

        public void PutGap(string checkpointId, ulong gap) {
            _gaps[checkpointId] = gap;
        }

        public ulong GetGap(string checkpointId) => _gaps[checkpointId];
    }
}
