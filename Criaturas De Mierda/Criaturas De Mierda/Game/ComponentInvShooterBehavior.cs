using Engine;
using GameEntitySystem;
using System.Collections.Generic;
using TemplatesDatabase;
using System; // Añadir para Math.Pow

namespace Game
{
    public class ComponentInvShooterBehavior : ComponentBehavior, IUpdateable
    {
        public ComponentCreature m_componentCreature;
        public ComponentChaseBehavior m_componenttChaseBehavior;
        public SubsystemTerrain m_subsystemTerrain;
        public StateMachine m_stateMachine = new StateMachine();
        public SubsystemTime m_subsystemTime;
        public SubsystemProjectiles m_subsystemProjectiles;
        public Random m_random = new Random();
        public int m_arrowValue;
        public double m_nextUpdateTime;
        public double m_ChargeTime;
        public float m_distance;
        public int UpdateOrder => 0;
        public override float ImportanceLevel => 0f;
        public bool DiscountFromInventory;
        public string MinMaxRandomChargeTime;
        public float m_randomThrowMin;
        public float m_randomThrowMax;
        public SubsystemAudio m_subsystemAudio;
        public string ThrowingSound;
        public float ThrowingSoundDistance;
        public bool SelectRandomThrowableItems;
        public string SpecialThrowableItem;
        public int m_specialThrowableItemValue;
        public List<int> m_specialThrowableItemValues = new List<int>();
        public float m_minDistance;   // Nueva variable para distancia mínima
        public float m_maxDistance;   // Nueva variable para distancia máxima
        public string MinMaxDistance;
        public float m_randomWaitMin; // variable para tiempo minimo de espera para el tiempo de carga del proyectil
        public float m_randomWaitMax; // variable para tiempo minimo de espera para el tiempo de carga del proyectil
        public string MinMaxRandomWaitTime;
        public double m_chargeStartTime;
        public bool m_isCharging;
        public float m_chargeDuration;
        public bool ThrowFromHead;
        public ComponentCreatureModel m_componentModel;
        UpdateOrder IUpdateable.UpdateOrder => ((IUpdateable)m_subsystemProjectiles).UpdateOrder;
        public List<int> m_excludedItems = new List<int>(); // Nueva lista para almacenar los items excluidos
        public ComponentInventory m_componentInventory;

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_componentCreature = base.Entity.FindComponent<ComponentCreature>(throwOnError: true);
            m_componenttChaseBehavior = base.Entity.FindComponent<ComponentChaseBehavior>(throwOnError: true);
            m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(throwOnError: true);
            m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(throwOnError: true);
            m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(throwOnError: true);
            m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(throwOnError: true);
            m_componentInventory = base.Entity.FindComponent<ComponentInventory>(throwOnError: true); // Obtener el inventario
            m_componentModel = base.Entity.FindComponent<ComponentCreatureModel>(throwOnError: false);
            this.ThrowingSound = valuesDictionary.GetValue<string>("ThrowingSound"); // Genera un sonido cuando la criatura arroje el objeto. ejemplo: Audio/Throw
            this.ThrowingSoundDistance = valuesDictionary.GetValue<float>("ThrowingSoundDistance"); // Rango de distancia para escuchar el sonido.
            this.DiscountFromInventory = valuesDictionary.GetValue<bool>("DiscountFromInventory"); // Descontar objetos del inventario por cada objeto arrojado si el valor es true.
            this.MinMaxDistance = valuesDictionary.GetValue<string>("MinMaxDistance"); // Distancia minima y maxima para que la criatura arroje el objeto, los valores se representan de este modo Min:Max ejemplo: 2:16.
            this.MinMaxRandomChargeTime = ValuesDictionary.GetValue<string>("MinMaxRandomChargeTime"); // Establece un intervalo de tiempo en segundos minimo y maximo determinado para que la criatura arroje el objeto en un momento aleatorio dentro de ese rango de tiempo, deje una relacion 1:1 para que no sea aleatorio, e.g 2:2.
            this.MinMaxRandomWaitTime = ValuesDictionary.GetValue<string>("MinMaxRandomWaitTime"); // Tiempo de espera en segundos aleatorio para que la criatura empiece a cargar el tiempo de la fuerza del proyectil, los valores se representan en Min:Max ejemplo: 1:10.
            this.SelectRandomThrowableItems = ValuesDictionary.GetValue<bool>("SelectRandomThrowableItems"); // La criatura selecciona objetos aleatoriamente que se puedan lanzar de su inventario si es true.
            this.ThrowFromHead = ValuesDictionary.GetValue<bool>("ThrowFromHead"); // Los objetos salen arrojados de la caveza de la criatura si el valor es true.
            string excludedItemsString = valuesDictionary.GetValue<string>("ExcludedThrowableItems", string.Empty);
            // Parsear SpecialThrowableItem (ahora puede contener múltiples valores separados por comas y variantes con dos puntos)
            string specialThrowableItem = ValuesDictionary.GetValue<string>("SpecialThrowableItem", string.Empty);
            if (!string.IsNullOrEmpty(specialThrowableItem))
            {
                // Dividir por comas
                string[] items = specialThrowableItem.Split(',');
                foreach (string item in items)
                {
                    string trimmedItem = item.Trim();

                    // Verificar si el formato incluye variante (ejemplo: "ArrowBlock:1")
                    if (trimmedItem.Contains(":"))
                    {
                        string[] parts = trimmedItem.Split(':');
                        string blockName = parts[0].Trim();
                        string variantString = parts[1].Trim();

                        // Intentar parsear la variante como número
                        if (int.TryParse(variantString, out int variant))
                        {
                            int blockIndex = BlocksManager.GetBlockIndex(blockName, throwIfNotFound: false);
                            if (blockIndex >= 0)
                            {
                                // Crear el valor del bloque con la variante especificada
                                int blockValue = Terrain.MakeBlockValue(blockIndex, 0, variant);
                                m_specialThrowableItemValues.Add(blockValue);
                            }
                            else
                            {
                                Log.Warning($"SpecialThrowableItem '{trimmedItem}' not found");
                            }
                        }
                        else
                        {
                            Log.Warning($"SpecialThrowableItem '{trimmedItem}' has invalid variant format");
                        }
                    }
                    else
                    {
                        // Comportamiento original para bloques sin variante
                        if (int.TryParse(trimmedItem, out int blockId))
                        {
                            // Si es un número, tratar como ID de bloque
                            m_specialThrowableItemValues.Add(Terrain.MakeBlockValue(blockId));
                        }
                        else
                        {
                            // Si es texto, tratar como nombre de clase
                            int blockIndex = BlocksManager.GetBlockIndex(trimmedItem, throwIfNotFound: false);
                            if (blockIndex >= 0)
                            {
                                m_specialThrowableItemValues.Add(Terrain.MakeBlockValue(blockIndex));
                            }
                            else
                            {
                                // Bloque no encontrado
                                Log.Warning($"SpecialThrowableItem '{trimmedItem}' not found");
                            }
                        }
                    }
                }
            }
            else
            {
                // Si está vacío, no hay objetos especiales
                m_specialThrowableItemValues.Clear();
            }

            if (!string.IsNullOrEmpty(excludedItemsString))
            {
                string[] excludedItems = excludedItemsString.Split(';');
                foreach (string item in excludedItems)
                {
                    if (int.TryParse(item, out int blockId))
                    {
                        m_excludedItems.Add(Terrain.ExtractContents(Terrain.MakeBlockValue(blockId)));
                    }
                    else
                    {
                        int blockIndex = BlocksManager.GetBlockIndex(item, throwIfNotFound: false);
                        if (blockIndex >= 0)
                        {
                            m_excludedItems.Add(blockIndex);
                        }
                        else
                        {
                            Log.Warning($"ExcludedThrowableItem '{item}' not found");
                        }
                    }
                }
            }
            // Parsear MinMaxRandomWaitTime
            string[] waitTimes = this.MinMaxRandomWaitTime.Split(';');
            if (waitTimes.Length >= 2 &&
                float.TryParse(waitTimes[0], out m_randomWaitMin) &&
                float.TryParse(waitTimes[1], out m_randomWaitMax))
            {
                // Valores válidos
            }
            else
            {
                // Valores por defecto si hay error
                m_randomWaitMin = 1f;
                m_randomWaitMax = 10f;
                Log.Warning($"Invalid MinMaxRandomWaitTime format or empty: '{MinMaxRandomWaitTime}'. Using default values ('{m_randomWaitMin}';'{m_randomWaitMax}').");
            }

            // Parsear MinMaxDistance
            string[] distances = this.MinMaxDistance.Split(';');
            if (distances.Length >= 2 &&
                float.TryParse(distances[0], out m_minDistance) &&
                float.TryParse(distances[1], out m_maxDistance))
            {
                // Valores válidos, mantenerlos
            }
            else
            {
                // Valores por defecto si hay error
                m_minDistance = 2f;
                m_maxDistance = 16f;
                Log.Warning($"Invalid MinMaxDistance format: '{MinMaxDistance}'. Using default values ('{m_minDistance}';'{m_maxDistance}').");
            }

            // Parsear SpecialThrowableItem
            if (!string.IsNullOrEmpty(SpecialThrowableItem))
            {
                if (int.TryParse(SpecialThrowableItem, out int blockId))
                {
                    // Si es un número, tratar como ID de bloque
                    m_specialThrowableItemValue = Terrain.MakeBlockValue(blockId);
                }
                else
                {
                    // Si es texto, tratar como nombre de clase
                    int blockIndex = BlocksManager.GetBlockIndex(SpecialThrowableItem, throwIfNotFound: false);
                    if (blockIndex >= 0)
                    {
                        m_specialThrowableItemValue = Terrain.MakeBlockValue(blockIndex);
                    }
                    else
                    {
                        // Bloque no encontrado
                        m_specialThrowableItemValue = 0;
                        Log.Warning($"SpecialThrowableItem '{SpecialThrowableItem}' not found");
                    }
                }
            }
            else
            {
                m_specialThrowableItemValue = 0;
            }

            string[] times = this.MinMaxRandomChargeTime.Split(';');
            if (times.Length >= 2 &&
                float.TryParse(times[0], out m_randomThrowMin) &&
                float.TryParse(times[1], out m_randomThrowMax))
            {
                // Valores válidos, mantenerlos
            }
            else
            {
                // Valores por defecto si hay error
                m_randomThrowMin = 1.6f;
                m_randomThrowMax = 2.2f;
                Log.Warning($"Invalid MinMaxRandomWaitTime format: '{MinMaxRandomWaitTime}'. Using default values ('{m_randomThrowMin}';'{m_randomThrowMax}').");
            }
        }

        public void Update(float dt)
        {
            // Verificar si la criatura está muerta (salud <= 0)
            if (m_componentCreature.ComponentHealth.Health <= 0f)
            {
                // Si está muerta, no ejecutar el comportamiento de lanzamiento
                return;
            }

            double currentTime = m_subsystemTime.GameTime;

            // Si estamos en medio de una carga
            if (m_isCharging)
            {
                // Verificar si el objetivo sigue existiendo y calcular distancia actual
                bool targetValid = m_componenttChaseBehavior.Target != null;
                float currentDistance = 0f;

                if (targetValid)
                {
                    Vector3 vector;
                    if (ThrowFromHead)
                    {
                        vector = m_componentCreature.ComponentCreatureModel.EyePosition;
                    }
                    else
                    {
                        vector = m_componentCreature.ComponentCreatureModel.EyePosition +
                            m_componentCreature.ComponentBody.Matrix.Right * 0.3f -
                            m_componentCreature.ComponentBody.Matrix.Up * 0.2f +
                            m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
                    }

                    currentDistance = Vector3.Distance(vector,
                        m_componenttChaseBehavior.Target.ComponentBody.Position);
                }

                // Cancelar carga si:
                // 1. El objetivo ya no es válido
                // 2. La distancia está fuera del rango permitido
                // 3. El objetivo murió o desapareció
                if (!targetValid ||
                    currentDistance < m_minDistance ||
                    currentDistance > m_maxDistance ||
                    (m_componenttChaseBehavior.Target.Entity.FindComponent<ComponentHealth>()?.Health <= 0f) == true)
                {
                    // Eliminado: JawFactor no existe en ComponentComboModel
                    m_isCharging = false;
                    return;
                }

                // Verificar si ha terminado el tiempo de carga
                if (currentTime >= m_chargeStartTime + m_chargeDuration)
                {
                    // Disparar el proyectil
                    FireProjectile();

                    // Terminar el estado de carga
                    m_isCharging = false;

                    // Programar próxima actualización
                    m_ChargeTime = m_random.Float(m_randomThrowMin, m_randomThrowMax);
                    if (m_distance < m_minDistance)
                    {
                        m_ChargeTime *= 0.9;
                    }
                    // Eliminado: JawFactor no existe en ComponentComboModel

                    m_nextUpdateTime = currentTime + m_ChargeTime;
                }
                return;
            }

            // Comportamiento normal si no estamos cargando
            if (currentTime >= m_nextUpdateTime)
            {
                // Buscar un objeto lanzable válido
                m_arrowValue = FindAimableItemInInventory();
                bool hasValidItem = m_arrowValue != 0;

                if (hasValidItem && m_componenttChaseBehavior.Target != null)
                {
                    Vector3 vector;
                    if (ThrowFromHead)
                    {
                        // Lanzar desde la cabeza (posición del ojo)
                        vector = m_componentCreature.ComponentCreatureModel.EyePosition;
                    }
                    else
                    {
                        // Lanzar desde la posición original (lado del cuerpo)
                        vector = m_componentCreature.ComponentCreatureModel.EyePosition +
                            m_componentCreature.ComponentBody.Matrix.Right * 0.3f -
                            m_componentCreature.ComponentBody.Matrix.Up * 0.2f +
                            m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
                    }
                    Vector3 vector2 = m_componenttChaseBehavior.Target.ComponentBody.Position - vector;
                    m_distance = vector2.Length();

                    // Verificar rango de distancia
                    if (m_distance >= m_minDistance && m_distance <= m_maxDistance)
                    {
                        // Iniciar secuencia de carga
                        m_isCharging = true;
                        m_chargeDuration = m_random.Float(m_randomWaitMin, m_randomWaitMax);
                        m_chargeStartTime = currentTime;
                        // Eliminado: JawFactor no existe en ComponentComboModel
                    }
                }
                else
                {
                    // Programar próxima actualización si no hay objetivo válido
                    m_ChargeTime = m_random.Float(m_randomThrowMin, m_randomThrowMax);
                    if (m_distance < m_minDistance)
                    {
                        m_ChargeTime *= 0.9;
                    }
                    m_nextUpdateTime = currentTime + m_ChargeTime;
                }

                m_stateMachine.Update();

                // Resetear animación después de disparar
                if (!m_isCharging && m_componentModel != null)
                {
                    if (m_componentModel is ComponentHumanModel humanModel)
                    {
                        humanModel.m_handAngles2 = Vector2.Lerp(
                            humanModel.m_handAngles2,
                            new Vector2(0f, humanModel.m_handAngles2.Y),
                            5f * dt
                        );
                    }
                }
            }
        }

        private void FireProjectile()
        {
            Vector3 vector = m_componentCreature.ComponentCreatureModel.EyePosition +
            m_componentCreature.ComponentBody.Matrix.Right * 0.3f -
            m_componentCreature.ComponentBody.Matrix.Up * 0.2f +
            m_componentCreature.ComponentBody.Matrix.Forward * 0.2f;
            Vector3 vector2 = m_componenttChaseBehavior.Target.ComponentBody.Position - vector;
            m_distance = vector2.Length();
            Vector3 vector3 = Vector3.Normalize(vector2 + m_random.Vector3((m_distance < 10f) ? 0.4f : 1f));

            // CORREGIDO: Reemplazar MathUtils.Pow obsoleto por Math.Pow
            float num = MathUtils.Lerp(0f, 40f, (float)Math.Pow((float)m_ChargeTime / 4f, 1f));

            // Disparar el proyectil
            m_subsystemProjectiles.FireProjectile(
                m_arrowValue,
                vector,
                vector3 * num + new Vector3(0f, m_random.Float(5f, 8f) * m_distance / num, 0f),
                Vector3.Zero,
                m_componentCreature
            );

            // Activar animación
            if (m_componentModel != null)
            {
                if (m_componentModel is ComponentHumanModel humanModel)
                {
                    // Mover brazo derecho en ComponentHumanModel
                    humanModel.m_handAngles2 = new Vector2(
                    MathUtils.DegToRad(-90f),
                    humanModel.m_handAngles2.Y
                    );
                }
            }

            // Reproducir sonido de lanzamiento
            if (!string.IsNullOrEmpty(ThrowingSound))
            {
                float pitch = m_random.Float(-0.1f, 0.1f);
                m_subsystemAudio.PlaySound(
                    ThrowingSound,
                    1f,
                    pitch,
                    vector,
                    ThrowingSoundDistance,
                    0.1f
                );
            }

            // Descontar del inventario si está habilitado
            if (DiscountFromInventory)
            {
                RemoveAimableItemFromInventory(m_arrowValue);
            }
        }

        private int FindAimableItemInInventory()
        {
            // 1. Si hay objetos especiales configurados, elegir uno aleatoriamente
            if (m_specialThrowableItemValues.Count > 0)
            {
                int index = m_random.Int(0, m_specialThrowableItemValues.Count - 1);
                return m_specialThrowableItemValues[index];
            }

            // 2. Comportamiento normal si no hay objetos especiales
            if (m_componentInventory == null) return 0;

            List<int> throwableItems = new List<int>();

            for (int i = 0; i < m_componentInventory.SlotsCount; i++)
            {
                int slotValue = m_componentInventory.GetSlotValue(i);
                if (slotValue != 0)
                {
                    int content = Terrain.ExtractContents(slotValue);
                    // Saltar si el item está en la lista de excluidos
                    if (m_excludedItems.Contains(content))
                        continue;

                    Block block = BlocksManager.Blocks[content];
                    if (block.IsAimable_(slotValue))
                    {
                        if (SelectRandomThrowableItems)
                        {
                            throwableItems.Add(slotValue);
                        }
                        else
                        {
                            return slotValue;
                        }
                    }
                }
            }

            // Seleccionar aleatoriamente si hay objetos disponibles
            if (throwableItems.Count > 0)
            {
                int randomIndex = m_random.Int(0, throwableItems.Count - 1);
                return throwableItems[randomIndex];
            }

            return 0;
        }
        private void RemoveAimableItemFromInventory(int value)
        {
            if (m_componentInventory == null) return;

            for (int i = 0; i < m_componentInventory.SlotsCount; i++)
            {
                if (m_componentInventory.GetSlotValue(i) == value &&
                    m_componentInventory.GetSlotCount(i) > 0)
                {
                    // Usar el método oficial para remover items
                    m_componentInventory.RemoveSlotItems(i, 1);
                    break;
                }
            }
        }
    }
}
