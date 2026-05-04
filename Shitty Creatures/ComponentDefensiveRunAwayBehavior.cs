using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    public class ComponentDefensiveRunAwayBehavior : ComponentBehavior, IUpdateable
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

        public virtual void Update(float dt)
        {
            // No hace nada - la criatura no reacciona a ningún tipo de daño, ruido o amenaza
        }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            // Se lee el valor de la base de datos solo para evitar que el juego lance un error
            // si la plantilla (template) de la criatura tiene definido este parámetro, 
            // pero no se utiliza en ninguna parte de la lógica.
            this.LowHealthToEscape = valuesDictionary.GetValue<float>("LowHealthToEscape", 0.33f);
        }
    }
}
