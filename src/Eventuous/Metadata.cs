using System.Collections.Generic;
using JetBrains.Annotations;

namespace Eventuous {
    [PublicAPI]
    public class Metadata : Dictionary<string, object> {
        public Metadata() { }

        public Metadata(IDictionary<string, object> dictionary) : base(dictionary) { }
    }
}