using System;
using System.Collections.Generic;
using UnityEngine;
namespace Yuki.Learning.StateMachine.ScriptableObjects
{
    public class ExposedParametersSO : ScriptableObject
    {
        [SerializeField] private List<ExposedParameter> _parameters = new List<ExposedParameter>();
        public IReadOnlyList<ExposedParameter> Parameters => _parameters;
    }

    [Serializable]
    public class ExposedParameter
    {
        [SerializeField] private string _name;
        [SerializeField] private List<ExposedBinding> _bindings = new List<ExposedBinding>();

        [SerializeField] private string _category;
        
        public string Name => _name;
        public IReadOnlyList<ExposedBinding> Bindings => _bindings;
        public string Category => _category;

    }

    [Serializable]
    public class ExposedBinding
    {
        [SerializeField] private StateElementSO _target;
        [SerializeField] private string _fieldName;

        public StateElementSO Target => _target;
        public string FieldName => _fieldName;
    }
}