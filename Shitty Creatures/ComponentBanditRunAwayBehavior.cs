using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    public class ComponentBanditRunAwayBehavior : ComponentBehavior, IUpdateable
    {
        public UpdateOrder UpdateOrder
        {
            get
            {
                return UpdateOrder.Default;
            }
        }

        public override float ImportanceLevel
        {
            get
            {
                return 0f;
            }
        }

        public virtual void Update(float dt)
        {
        }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
        }
    }
}
