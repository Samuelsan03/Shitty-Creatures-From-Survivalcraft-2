using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    /// <summary>
    /// Subsystem que maneja la reproducción de música y alertas cuando un Tank (o variantes)
    /// persigue a un jugador.
    /// </summary>
    public class SubsystemTankChaseMusic : Subsystem, IUpdateable
    {
        private static readonly HashSet<string> TankEntityNames = new HashSet<string>
        {
            "Tank1", "Tank2", "Tank3",
            "TankGhost1", "TankGhost2", "TankGhost3",
            "FrozenTank", "FrozenTankGhost"
        };

        private SubsystemAudio m_subsystemAudio;
        private SubsystemTime m_subsystemTime;
        private SubsystemPlayers m_subsystemPlayers;

        private HashSet<Entity> m_activeChasingTanks = new HashSet<Entity>();
        private Dictionary<Entity, bool> m_alertedForTank = new Dictionary<Entity, bool>();
        private bool m_isMusicPlaying = false;
        private double m_musicStartTime = 0;
        private const double TANK_MUSIC_DURATION = 52.0;

        // Radio de distancia para considerar que el jugador está "cerca" del Tank
        private const float CHASE_RADIUS = 40f;

        private const string TankMusicPath = "MenuMusic/ChaseTheme/Tank Theme";
        private const string TankAlertSoundPath = "Audio/UI/Tank Warning Sound";

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public override void Load(ValuesDictionary valuesDictionary)
        {
            m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
            m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
            m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
        }

        public void Update(float dt)
        {
            // Detener música si no estamos en la pantalla de juego (menú, loading, etc.)
            if (!(ScreensManager.CurrentScreen is GameScreen))
            {
                if (m_isMusicPlaying)
                {
                    InGameMusicManager.StopMusic();
                    m_isMusicPlaying = false;
                }
                return;
            }

            // Si el proyecto o el subsistema de tiempo son nulos (mundo descargado), no hacer nada
            if (Project == null || m_subsystemTime == null || m_subsystemPlayers == null)
                return;

            // Si no hay jugadores, detener música
            if (m_subsystemPlayers.ComponentPlayers.Count == 0)
            {
                if (m_isMusicPlaying)
                {
                    InGameMusicManager.StopMusic();
                    m_isMusicPlaying = false;
                }
                return;
            }

            // Si el juego está pausado, no hacer nada
            if (m_subsystemTime.GameTimeFactor == 0f)
                return;

            // Obtener la posición del primer jugador (o del jugador más cercano al Tank)
            ComponentPlayer closestPlayer = null;
            float closestDistance = float.MaxValue;

            var chasingNow = new HashSet<Entity>();

            foreach (var entity in Project.Entities)
            {
                string entityName = entity.ValuesDictionary?.DatabaseObject?.Name;
                if (string.IsNullOrEmpty(entityName) || !TankEntityNames.Contains(entityName))
                    continue;

                var health = entity.FindComponent<ComponentHealth>();
                if (health == null || health.Health <= 0f)
                    continue;

                ComponentChaseBehavior chaseBehavior = entity.FindComponent<ComponentZombieChaseBehavior>();
                if (chaseBehavior == null)
                    chaseBehavior = entity.FindComponent<ComponentChaseBehavior>();

                if (chaseBehavior == null)
                    continue;

                ComponentCreature target = chaseBehavior.Target;
                if (target == null)
                    continue;

                var playerTarget = target as ComponentPlayer;
                if (playerTarget == null || playerTarget.ComponentHealth.Health <= 0f)
                    continue;

                if (chaseBehavior.Suppressed)
                    continue;

                // Verificar distancia real entre el Tank y el jugador
                float distance = Vector3.Distance(
                    entity.FindComponent<ComponentBody>()?.Position ?? Vector3.Zero,
                    playerTarget.ComponentBody?.Position ?? Vector3.Zero
                );

                // Solo considerar si está dentro del radio de persecución
                if (distance > CHASE_RADIUS)
                    continue;

                chasingNow.Add(entity);

                // Guardar el jugador más cercano para la alerta
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlayer = playerTarget;
                }

                if (!m_alertedForTank.ContainsKey(entity) || !m_alertedForTank[entity])
                {
                    if (closestPlayer != null)
                        TriggerAlertAndMessage(closestPlayer);
                    m_alertedForTank[entity] = true;
                }
            }

            // Limpiar entidades que ya no persiguen o están fuera del radio
            var toRemove = new List<Entity>();
            foreach (var kv in m_alertedForTank)
            {
                if (!chasingNow.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }
            foreach (var e in toRemove)
                m_alertedForTank.Remove(e);

            m_activeChasingTanks = chasingNow;

            // La música suena SOLO si hay al menos un Tank persiguiendo a un jugador dentro del radio
            bool shouldPlayMusic = (m_activeChasingTanks.Count > 0) && ShittyCreaturesSettingsManager.TankMusicEnabled;

            if (shouldPlayMusic)
            {
                if (!m_isMusicPlaying)
                {
                    InGameMusicManager.PlayMusic(TankMusicPath, 0f);
                    m_musicStartTime = Time.RealTime;
                    m_isMusicPlaying = true;
                }
                else
                {
                    double elapsed = Time.RealTime - m_musicStartTime;
                    if (elapsed >= TANK_MUSIC_DURATION)
                    {
                        InGameMusicManager.PlayMusic(TankMusicPath, 0f);
                        m_musicStartTime = Time.RealTime;
                    }
                }
            }
            else
            {
                if (m_isMusicPlaying)
                {
                    InGameMusicManager.StopMusic();
                    m_isMusicPlaying = false;
                }
            }
        }

        private void TriggerAlertAndMessage(ComponentPlayer targetPlayer)
        {
            if (m_subsystemAudio != null)
            {
                m_subsystemAudio.PlaySound(TankAlertSoundPath, 1f, 0f, 0f, 0f);
            }

            if (targetPlayer.ComponentGui != null)
            {
                bool translationFound;
                string message = LanguageControl.Get(out translationFound, "Messages", "TankChaseAlert");
                if (translationFound)
                {
                    targetPlayer.ComponentGui.DisplaySmallMessage(message, Color.Red, true, true);
                }
            }
        }

        public override void Dispose()
        {
            if (m_isMusicPlaying)
            {
                InGameMusicManager.StopMusic();
                m_isMusicPlaying = false;
            }
            m_activeChasingTanks?.Clear();
            m_alertedForTank?.Clear();
            base.Dispose();
        }
    }
}
