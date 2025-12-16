using UnityEngine;
using Meta.XR.MRUtilityKit;

public class snapToDesk : MonoBehaviour
{
    [SerializeField] private GameObject objectToAnchor;
    [SerializeField] private float yOffset = 0.02f;
    [SerializeField] private string tableLabel = "TABLE";

    private void Start()
    {
        TrySnap();
        MRUK.Instance.RoomCreatedEvent.AddListener(OnRoomCreated);
    }

    private void OnRoomCreated(MRUKRoom room) => TrySnap();

    private void TrySnap()
    {
        var room = MRUK.Instance.GetCurrentRoom();
        if (room == null || objectToAnchor == null) return;

        Transform camT = GetHmdTransform();
        if (camT == null) return;

        MRUKAnchor nearest = FindNearestTable(room, camT.position);
        if (nearest == null) { Debug.LogWarning("No table found."); return; }

        if (!TryGetBounds(nearest, out var b)) { Debug.LogWarning("Table has no bounds."); return; }

        Vector3 topCenter = new Vector3(b.center.x, b.max.y, b.center.z);
        objectToAnchor.transform.position = topCenter + Vector3.up * yOffset;
        objectToAnchor.transform.rotation = Quaternion.Euler(0f, nearest.transform.eulerAngles.y, 0f);

        // snap once, then stop listening
        MRUK.Instance.RoomCreatedEvent.RemoveListener(OnRoomCreated);
    }

    private MRUKAnchor FindNearestTable(MRUKRoom room, Vector3 camPos)
    {
        MRUKAnchor best = null;
        float bestDist = float.PositiveInfinity;

        foreach (var a in room.Anchors)
        {
            if (a == null) continue;
            if (!a.HasLabel(tableLabel)) continue; // only tables

            if (!TryGetBounds(a, out var b)) continue;

            // Distance to the *bounds*, so it works even if anchor pivot is off
            float d = Vector3.Distance(camPos, b.ClosestPoint(camPos));

            if (d < bestDist)
            {
                bestDist = d;
                best = a;
            }
        }

        return best;
    }

    private bool TryGetBounds(MRUKAnchor anchor, out Bounds bounds)
    {
        var r = anchor.GetComponentInChildren<Renderer>();
        if (r != null) { bounds = r.bounds; return true; }

        var c = anchor.GetComponentInChildren<Collider>();
        if (c != null) { bounds = c.bounds; return true; }

        bounds = default;
        return false;
    }

    private Transform GetHmdTransform()
    {
        // Prefer OVRCameraRig if present
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null && rig.centerEyeAnchor != null) return rig.centerEyeAnchor;

        // Fallback
        if (Camera.main != null) return Camera.main.transform;

        return null;
    }
}
