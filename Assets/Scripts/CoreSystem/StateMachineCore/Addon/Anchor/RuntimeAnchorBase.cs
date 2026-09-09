using UnityEngine;
namespace Yuki.Learning.StateMachine
{
    public abstract class RuntimeAnchorBase<T> : ScriptableObject where T : UnityEngine.Object
    {
        private T _value;

        public T Value => _value;

        public bool IsSet => _value != null;

        public void Provide(T value)
        {
            if (value == null)
            {
                Debug.LogError(
                    $"ไม่สามารถใส่ null ลงใน {name}");

                return;
            }

            _value = value;
        }

        public void Unset()
        {
            _value = null;
        }

        private void OnDisable()
        {
            Unset();
        }
    }
}
