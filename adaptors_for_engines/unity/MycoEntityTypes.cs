using System;
using Mycology_ECS.translation_layers.unity.entity_helpers;
using UnityEngine;

namespace Mycology_ECS.translation_layers.unity
{
    internal sealed class ManagedEntity
    {
        public string entityId;
        public string entityType;
        public bool isPooled;
        public MycoGameObjectPool pool;
        public GameObject singletonInstance;
        public EntityDef definition;

        public bool isUiLayer;
        public int uiSortOrder;
    }

    [Serializable]
    internal sealed class EntitiesRoot
    {
        public EntityDef[] entities;
    }

    [Serializable]
    internal sealed class EntityDef
    {
        public string id;
        public string name;
        public string type;
        public bool create_on_start;

        public string[] components;

        public bool object_pooling;
        public int pool_initial_size;
        public int pool_max_size;

        public UI.UiImageDef UI_image;
        public UI.UiTextDef UI_text;
    }
}
