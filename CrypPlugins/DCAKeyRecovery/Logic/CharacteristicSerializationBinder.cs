/*
   Copyright 2026 CrypTool 2 Team

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DCAKeyRecovery.Logic
{
    /// <summary>
    /// Type names in attack configurations ("$type"): the lists of characteristics hold subclasses of the abstract
    /// <see cref="Characteristic"/> (one per toy cipher), so the JSON names the concrete class. Only those classes are
    /// accepted - a configuration cannot make the deserializer create any other type. The class is looked up in this
    /// plugin by its full name; the assembly name in the JSON is not used.
    /// </summary>
    public class CharacteristicSerializationBinder : ISerializationBinder
    {
        public Type BindToType(string assemblyName, string typeName)
        {
            Type type = typeof(Characteristic).Assembly.GetType(typeName, false);
            if (type == null || !typeof(Characteristic).IsAssignableFrom(type) || type.IsAbstract)
            {
                throw new JsonSerializationException("Type not allowed in a configuration: " + typeName);
            }
            return type;
        }

        public void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = serializedType.Assembly.GetName().Name;
            typeName = serializedType.FullName;
        }
    }
}
