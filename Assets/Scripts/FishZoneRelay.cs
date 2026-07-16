using UnityEngine;

public enum FishZoneType { Outer, Inner }

// Sits on a fish's OuterZone/InnerZone trigger-collider child. Unity's trigger callback doesn't
// say which of a GameObject's own multiple colliders fired, so each zone needs its own dedicated
// child GameObject relaying back to the parent CatchableFish, rather than one script handling both.
public class FishZoneRelay : MonoBehaviour
{
    [SerializeField] private CatchableFish owner;
    [SerializeField] private FishZoneType zoneType;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<FlightController>() != null)
            owner.OnZoneEnter(zoneType, other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<FlightController>() != null)
            owner.OnZoneExit(zoneType, other);
    }
}
