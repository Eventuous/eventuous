using System.Diagnostics;

namespace Eventuous.Diagnostics; 

public interface IWithCustomTags {
    void SetCustomTags(TagList customTags);
}
