using System;

namespace Patches {
    public class BoxedArray {
        public int Length => Raw.Length;
        
        public Array Raw;
        
        public object this[int index] {
            get => Raw.GetValue(index);
            set => Raw.SetValue(value, index);
        }
        
        public BoxedArray(object raw) {
            Raw = (Array)raw;
        }
        
        public BoxedArray(Array raw) {
            Raw = raw;
        }
        
        public void RemoveAt(int index) {
            var newArray = new object[Raw.Length - 1];
            Array.Copy(Raw, 0, newArray, 0, index);
            Array.Copy(Raw, index + 1, newArray, index, Raw.Length - index - 1);
            Raw = newArray;
        }
        
        public Array GetProperArray(Type type) {
            var array = Array.CreateInstance(type, Raw.Length);
            for (int i = 0; i < Raw.Length; i++) {
                array.SetValue(Convert.ChangeType(Raw.GetValue(i), type), i);
            }
            return array;
        }
    }
}
