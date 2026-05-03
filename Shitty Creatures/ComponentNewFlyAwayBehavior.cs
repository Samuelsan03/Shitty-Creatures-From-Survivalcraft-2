using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    public class ComponentNewFlyAwayBehavior : ComponentBehavior, IUpdateable
    {
        public float LowHealthToEscape { get; set; }

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

        public override bool IsActive
        {
            set
            {
                base.IsActive = value;
            }
        }

        public virtual void Update(float dt)
        {
            // No hace nada - la criatura no reacciona a nada
        }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
            this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
            this.LowHealthToEscape = valuesDictionary.GetValue<float>("LowHealthToEscape", 0.33f);
        }

        public SubsystemTime m_subsystemTime;
        public ComponentCreature m_componentCreature;
    }
}
