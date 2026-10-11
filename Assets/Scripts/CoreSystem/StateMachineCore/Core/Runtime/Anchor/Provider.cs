using System.Collections.Generic;
using UnityEngine;

namespace Yuki.Learning.StateMachine
{
    public class Provider : MonoBehaviour
    {
        [System.Serializable]
        private struct AnchorEntry
        {
            public AnchorIDSO anchorID;
            public GameObject tagetgameObject;
        }

        [SerializeField] private List<AnchorEntry> _anchorReferences 
            = new List<AnchorEntry>();
        private readonly Dictionary<AnchorIDSO, GameObject> _anchorDictionary 
            = new Dictionary<AnchorIDSO, GameObject>();

        public int Version { get; private set; }

        private void Awake()
        {
            foreach(var reference in _anchorReferences)
            {
                if (reference.anchorID == null)
                {
                    Debug.LogWarning("[Provider] A row has no AnchorID.", this);
                    continue;
                }
                if (reference.tagetgameObject == null)
                {
                    continue;
                }
                if (_anchorDictionary.ContainsKey(reference.anchorID))
                {
                    Debug.LogWarning
                    ($"[Provider] {reference.anchorID.name} is listed more than once. Keeping the first.", this);
                    continue;
                }
                
                _anchorDictionary.Add(reference.anchorID, reference.tagetgameObject);
                Version++;
            }
        }

        public bool TryGet(AnchorIDSO anchorIDSO, out GameObject targetGameObject)
        {
            if(anchorIDSO == null)
            {
                targetGameObject = null;
                return false; 
            }
            return _anchorDictionary.TryGetValue(anchorIDSO, out targetGameObject) && targetGameObject != null;
        }

        public void Set(AnchorIDSO anchorIDSO, GameObject target)
        {
            if (anchorIDSO == null)
            {
                Debug.LogWarning("[Provider] Set was called with a null AnchorID.", this);
                return;
            }
            if (target == null)
            {
                if (_anchorDictionary.Remove(anchorIDSO)) Version++;
                return;
            }

            _anchorDictionary[anchorIDSO] = target;
            Version++;
        }

    }
}
