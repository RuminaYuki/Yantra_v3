using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Sound/SoundTable")]
public class SoundTable : ScriptableObject
{
    public List<SoundLibrary> soundLibraries;
}