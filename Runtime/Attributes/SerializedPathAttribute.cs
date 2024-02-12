using System;
using UnityEngine;

namespace Nomnom.LCProjectPatcher.Attributes {
    public class SerializedPathAttribute: PropertyAttribute {
        public readonly string NameOfGetterFunction;
        
        public SerializedPathAttribute(string nameOfGetterFunction) {
            NameOfGetterFunction = nameOfGetterFunction;
        }
    }
}
