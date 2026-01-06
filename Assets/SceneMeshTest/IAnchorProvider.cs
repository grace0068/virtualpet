using System.Collections.Generic;
using Meta.XR.MRUtilityKit;

public interface IAnchorProvider
{
    List<MRUKAnchor> GetAnchors();
}
