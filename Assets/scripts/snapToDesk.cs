using UnityEngine;
using Meta.XR.MRUtilityKit;
using TMPro;

public class snapToDesk : MonoBehaviour
{
    [SerializeField] private GameObject objectToAnchor;
    [SerializeField] private float yOffset = 0.02f;
    [SerializeField] private string tableLabel = "WALL_FACE";
    [SerializeField] private TextMeshProUGUI debugText;
    
    private string debugLog = "";

    private void Start()
    {
        AddDebug("Start() called");
        AddDebug($"objectToAnchor: {(objectToAnchor != null ? objectToAnchor.name : "NULL")}");
        TrySnap();
        MRUK.Instance.RoomCreatedEvent.AddListener(OnRoomCreated);
    }

    private void AddDebug(string message)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
        debugLog = $"[{timestamp}] {message}\n{debugLog}";
        
        // Keep only last 20 lines
        string[] lines = debugLog.Split('\n');
        if (lines.Length > 20)
        {
            debugLog = string.Join("\n", lines, 0, 20);
        }
        
        if (debugText != null)
        {
            debugText.text = debugLog;
        }
        
        Debug.Log($"[snapToDesk] {message}");
    }

    private void OnRoomCreated(MRUKRoom room)
    {
        AddDebug("OnRoomCreated() called");
        TrySnap();
    }

    private void TrySnap()
    {
        AddDebug("TrySnap() called");
        var room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            AddDebug("ERROR: Room is NULL");
            return;
        }
        if (objectToAnchor == null)
        {
            AddDebug("ERROR: objectToAnchor is NULL");
            return;
        }
        AddDebug($"Room found: {room.name}");

        Transform camT = GetHmdTransform();
        if (camT == null)
        {
            AddDebug("ERROR: Camera transform is NULL");
            return;
        }
        AddDebug($"Camera position: {camT.position}");

        MRUKAnchor nearest = FindNearestOther(room, camT.position);
        if (nearest == null)
        {
            AddDebug("WARNING: No anchor found");
            return;
        }
        AddDebug($"Nearest anchor found: {nearest.name}");

        // Try to get bounds, but use transform position as fallback for wall anchors
        Vector3 finalPosition;
        if (TryGetBounds(nearest, out var b))
        {
            AddDebug($"Anchor bounds - center: {b.center}, max: {b.max}, size: {b.size}");
            Vector3 topCenter = new Vector3(b.center.x, b.max.y, b.center.z);
            finalPosition = topCenter + Vector3.up * yOffset;
        }
        else
        {
            // For wall anchors without bounds, use transform position
            AddDebug($"Using anchor transform position: {nearest.transform.position}");
            finalPosition = nearest.transform.position + Vector3.up * yOffset;
        }
        AddDebug($"Calculated position - topCenter: {topCenter}, finalPosition: {finalPosition}, yOffset: {yOffset}");
        
        objectToAnchor.transform.position = finalPosition;
        objectToAnchor.transform.rotation = Quaternion.Euler(0f, nearest.transform.eulerAngles.y, 0f);
        
        AddDebug($"Object snapped! Position: {objectToAnchor.transform.position}, Rotation: {objectToAnchor.transform.rotation.eulerAngles}");

        // snap once, then stop listening
        MRUK.Instance.RoomCreatedEvent.RemoveListener(OnRoomCreated);
    }

    private string GetAnchorClassification(MRUKAnchor anchor)
    {
        if (anchor == null) return null;
        
        // Try different properties that might contain the classification/type
        // Based on what we're printing in "All anchor names:", it's likely a.name
        // But check other possibilities too
        
        try
        {
            // Method 1: Check if a.name is the classification (most likely based on logs)
            // If a.name contains things like "WALL_FACE", "TABLE", etc., it's probably the classification
            string name = anchor.name;
            if (!string.IsNullOrEmpty(name) && 
                (name.Contains("WALL_FACE") || name.Contains("TABLE") || name.Contains("FLOOR") || 
                 name.Contains("CEILING") || name.Contains("OTHER") || name.Contains("BED")))
            {
                return name;
            }
            
            // Method 2: Try Classification property
            var classificationProp = anchor.GetType().GetProperty("Classification");
            if (classificationProp != null)
            {
                var val = classificationProp.GetValue(anchor);
                if (val != null) return val.ToString();
            }
            
            // Method 3: Try SceneLabel property
            var sceneLabelProp = anchor.GetType().GetProperty("SceneLabel");
            if (sceneLabelProp != null)
            {
                var val = sceneLabelProp.GetValue(anchor);
                if (val != null) return val.ToString();
            }
            
            // Method 4: Try AnchorLabel property
            var anchorLabelProp = anchor.GetType().GetProperty("AnchorLabel");
            if (anchorLabelProp != null)
            {
                var val = anchorLabelProp.GetValue(anchor);
                if (val != null) return val.ToString();
            }
            
            // Fallback: return name anyway
            return name;
        }
        catch
        {
            return anchor.name;
        }
    }
    
    private bool MatchesLabel(MRUKAnchor anchor, string searchLabel)
    {
        if (anchor == null || string.IsNullOrEmpty(searchLabel)) return false;
        
        // Method A: Use classification/type string (the thing we're printing)
        string classification = GetAnchorClassification(anchor);
        if (!string.IsNullOrEmpty(classification))
        {
            // Case-insensitive comparison
            if (classification.ToUpper().Trim() == searchLabel.ToUpper().Trim())
            {
                return true;
            }
        }
        
        // Method B: Try HasLabel as fallback (semantic labels)
        try
        {
            #pragma warning disable CS0618
            if (anchor.HasLabel(searchLabel))
            {
                return true;
            }
            #pragma warning restore CS0618
        }
        catch
        {
            // HasLabel failed, that's okay
        }
        
        return false;
    }

    private void TestAnchorLabel(MRUKAnchor anchor, string searchLabel)
    {
        if (anchor == null)
        {
            AddDebug("TEST: Anchor is NULL");
            return;
        }
        
        AddDebug($"=== TESTING ANCHOR: {anchor.name} ===");
        AddDebug($"Search label: '{searchLabel}' (length: {searchLabel.Length})");
        AddDebug($"Anchor name: '{anchor.name}' (length: {anchor.name.Length})");
        
        // Test 1: String comparison variations
        AddDebug($"--- String Comparisons ---");
        AddDebug($"Exact match: '{anchor.name}' == '{searchLabel}' = {anchor.name == searchLabel}");
        AddDebug($"Case-insensitive: '{anchor.name.ToUpper()}' == '{searchLabel.ToUpper()}' = {anchor.name.ToUpper() == searchLabel.ToUpper()}");
        AddDebug($"Trimmed: '{anchor.name.Trim()}' == '{searchLabel.Trim()}' = {anchor.name.Trim() == searchLabel.Trim()}");
        AddDebug($"Trimmed + Upper: '{anchor.name.Trim().ToUpper()}' == '{searchLabel.Trim().ToUpper()}' = {anchor.name.Trim().ToUpper() == searchLabel.Trim().ToUpper()}");
        
        // Test 2: HasLabel with different variations
        AddDebug($"--- HasLabel Tests ---");
        try
        {
            #pragma warning disable CS0618
            bool hasExact = anchor.HasLabel(searchLabel);
            bool hasUpper = anchor.HasLabel(searchLabel.ToUpper());
            bool hasLower = anchor.HasLabel(searchLabel.ToLower());
            bool hasTrimmed = anchor.HasLabel(searchLabel.Trim());
            #pragma warning restore CS0618
            
            AddDebug($"HasLabel('{searchLabel}') = {hasExact}");
            AddDebug($"HasLabel('{searchLabel.ToUpper()}') = {hasUpper}");
            AddDebug($"HasLabel('{searchLabel.ToLower()}') = {hasLower}");
            AddDebug($"HasLabel('{searchLabel.Trim()}') = {hasTrimmed}");
        }
        catch (System.Exception e)
        {
            AddDebug($"HasLabel error: {e.Message}");
        }
        
        // Test 3: Enum/Property checks
        AddDebug($"--- Property/Enum Checks ---");
        try
        {
            var labelSemanticProp = anchor.GetType().GetProperty("LabelSemantic");
            if (labelSemanticProp != null)
            {
                var labelSemantic = labelSemanticProp.GetValue(anchor);
                AddDebug($"LabelSemantic property: {labelSemantic} (type: {labelSemantic?.GetType().Name})");
            }
            else
            {
                AddDebug($"LabelSemantic property: NOT FOUND");
            }
            
            var anchorLabelProp = anchor.GetType().GetProperty("AnchorLabel");
            if (anchorLabelProp != null)
            {
                var anchorLabel = anchorLabelProp.GetValue(anchor);
                AddDebug($"AnchorLabel property: {anchorLabel} (type: {anchorLabel?.GetType().Name})");
            }
            else
            {
                AddDebug($"AnchorLabel property: NOT FOUND");
            }
            
            // Try to get all properties that might contain label info
            var allProps = anchor.GetType().GetProperties();
            foreach (var prop in allProps)
            {
                if (prop.Name.ToUpper().Contains("LABEL"))
                {
                    try
                    {
                        var val = prop.GetValue(anchor);
                        AddDebug($"Property '{prop.Name}': {val}");
                    }
                    catch { }
                }
            }
        }
        catch (System.Exception e)
        {
            AddDebug($"Property check error: {e.Message}");
        }
        
        AddDebug($"=== END TEST ===");
    }

    private MRUKAnchor FindNearestOther(MRUKRoom room, Vector3 camPos)
    {
        AddDebug($"FindNearestAnchor() - Finding closest anchor to camera");
        AddDebug($"Camera position: {camPos}");
        
        MRUKAnchor best = null;
        float bestDist = float.PositiveInfinity;
        int anchorCount = 0;

        // Use WallAnchors property like in the example code
        var anchorsToCheck = room.WallAnchors;
        AddDebug($"Checking {anchorsToCheck.Count} wall anchors");

        foreach (var a in anchorsToCheck)
        {
            anchorCount++;
            if (a == null)
            {
                AddDebug($"Anchor {anchorCount} is NULL");
                continue;
            }
            
            AddDebug($"Anchor {anchorCount}: {a.name}");

            // For wall anchors, we can use transform position directly
            // Try to get bounds first, but fallback to transform position
            float d;
            if (TryGetBounds(a, out var b))
            {
                // Distance to the *bounds*, so it works even if anchor pivot is off
                d = Vector3.Distance(camPos, b.ClosestPoint(camPos));
                AddDebug($"  -> Distance to bounds: {d:F2}m");
            }
            else
            {
                // Fallback: use transform position directly (works for wall anchors)
                d = Vector3.Distance(camPos, a.transform.position);
                AddDebug($"  -> Distance to position: {d:F2}m (using transform)");
            }

            if (d < bestDist)
            {
                bestDist = d;
                best = a;
                AddDebug($"  -> NEW CLOSEST: {a.name} at {d:F2}m");
            }
        }

        AddDebug($"Total anchors checked: {anchorCount}");
        
        if (best == null)
        {
            AddDebug("=== NO ANCHORS FOUND ===");
            AddDebug("No anchors with valid bounds found in room");
        }
        else
        {
            AddDebug($"=== CLOSEST ANCHOR FOUND ===");
            AddDebug($"Anchor: {best.name}");
            AddDebug($"Distance: {bestDist:F2}m");
        }

        return best;
    }

    private bool TryGetBounds(MRUKAnchor anchor, out Bounds bounds)
    {
        // Method 1: Try Renderer
        var r = anchor.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            bounds = r.bounds;
            AddDebug($"Got bounds from Renderer on {anchor.name}: {bounds}");
            return true;
        }

        // Method 2: Try Collider
        var c = anchor.GetComponentInChildren<Collider>();
        if (c != null)
        {
            bounds = c.bounds;
            AddDebug($"Got bounds from Collider on {anchor.name}: {bounds}");
            return true;
        }

        // Method 3: Try MRUK-specific properties (PlaneRect, etc.)
        try
        {
            var planeRectProp = anchor.GetType().GetProperty("PlaneRect");
            if (planeRectProp != null)
            {
                var planeRect = planeRectProp.GetValue(anchor);
                if (planeRect != null)
                {
                    // Try to get Rect from PlaneRect
                    var rectProp = planeRect.GetType().GetProperty("rect");
                    if (rectProp != null)
                    {
                        var rect = (Rect)rectProp.GetValue(planeRect);
                        Vector3 center = anchor.transform.position;
                        Vector3 size = new Vector3(rect.width, rect.height, 0.1f);
                        bounds = new Bounds(center, size);
                        AddDebug($"Got bounds from PlaneRect on {anchor.name}: center={center}, size={size}");
                        return true;
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            AddDebug($"Error getting PlaneRect: {e.Message}");
        }

        // Method 4: Fallback - use transform position with a small default size
        Vector3 position = anchor.transform.position;
        Vector3 defaultSize = new Vector3(1f, 1f, 0.1f); // Default size for wall anchors
        bounds = new Bounds(position, defaultSize);
        AddDebug($"Using fallback bounds for {anchor.name}: center={position}, size={defaultSize}");
        return true;
    }

    private Transform GetHmdTransform()
    {
        // Prefer OVRCameraRig if present
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null && rig.centerEyeAnchor != null)
        {
            AddDebug($"Using OVRCameraRig centerEyeAnchor");
            return rig.centerEyeAnchor;
        }

        // Fallback
        if (Camera.main != null)
        {
            AddDebug($"Using Camera.main");
            return Camera.main.transform;
        }

        AddDebug("ERROR: No camera found (neither OVRCameraRig nor Camera.main)");
        return null;
    }
}

