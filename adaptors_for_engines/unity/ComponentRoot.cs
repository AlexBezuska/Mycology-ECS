using System;
using System.Collections.Generic;

namespace Mycology_ECS.translation_layers.unity
{
    [Serializable]
    public class ComponentRoot
    {
        public List<Dictionary<string, object>> components;
    }
}
