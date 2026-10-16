using UnityEngine;

[AddComponentMenu("Inspector/Component Group Header")]
public class ComponentGroupHeader : MonoBehaviour
{
    public string groupName = "Group";
    public Color color = new Color(0.2f, 0.4f, 0.6f);
    public bool collapsed;
}
