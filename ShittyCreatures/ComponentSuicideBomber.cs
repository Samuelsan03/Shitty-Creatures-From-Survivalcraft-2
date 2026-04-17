using System;
using Engine;
using Engine.Audio;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentSuicideBomber : Component, IUpdateable
	{
		public SubsystemTime m_subsystemTime;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemExplosions m_subsystemExplosions;
		public ComponentHealth m_componentHealth;
		public ComponentBody m_componentBody;
		public ComponentSpawn m_componentSpawn;
		public ComponentChaseBehavior m_componentChaseBehavior;
		public float ExplosionPressure = 100f;
		public bool IsIncendiary = false;
		public float CountdownTime = 3f;
		private string CountdownSoundName = "Audio/Explosion De Mierda/Cuenta Regresiva Explosion";
		private string ExplosionSoundName = "Audio/Explosion De Mierda/Explosion Mejorada";
		private StateMachine m_stateMachine = new StateMachine();
		private float m_countdown;
		private Sound m_countdownSound;
		private bool m_isCountingDown;
		private float m_originalCorpseDuration;
		private bool m_hasPlayedCountdownSound;
		private bool m_exploded;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
			m_subsystemExplosions = Project.FindSubsystem<SubsystemExplosions>(true);
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentBody = Entity.FindComponent<ComponentBody>(true);
			m_componentSpawn = Entity.FindComponent<ComponentSpawn>(true);
			m_componentChaseBehavior = Entity.FindComponent<ComponentChaseBehavior>();
			ExplosionPressure = valuesDictionary.GetValue<float>("ExplosionPressure", 100f);
			IsIncendiary = valuesDictionary.GetValue<bool>("IsIncendiary", false);
			CountdownTime = valuesDictionary.GetValue<float>("CountdownTime", 3f);
			m_componentHealth.Injured += OnInjured;
			m_stateMachine.AddState("Idle", null, null, null);
			m_stateMachine.AddState("CountingDown", EnterCountingDown, UpdateCountingDown, LeaveCountingDown);
			m_stateMachine.TransitionTo("Idle");
			m_exploded = false;
		}

		private void OnInjured(Injury injury)
		{
			if (m_componentHealth.Health <= 0f && !m_isCountingDown && !m_exploded)
			{
				m_stateMachine.TransitionTo("CountingDown");
			}
		}

		private void EnterCountingDown()
		{
			m_isCountingDown = true;
			m_countdown = CountdownTime;
			m_hasPlayedCountdownSound = false;
			m_exploded = false;
			m_originalCorpseDuration = m_componentHealth.CorpseDuration;
			m_componentHealth.CorpseDuration = 10f;
			if (m_componentChaseBehavior != null && m_componentChaseBehavior.ImportanceLevel > 0f)
			{
				m_componentChaseBehavior.StopAttack();
			}
		}

		private void UpdateCountingDown()
		{
			m_countdown -= m_subsystemTime.GameTimeDelta;
			if (!m_hasPlayedCountdownSound)
			{
				try
				{
					m_countdownSound = m_subsystemAudio.CreateSound(CountdownSoundName);
					m_countdownSound.IsLooped = false;
					m_countdownSound.Play();
					m_hasPlayedCountdownSound = true;
				}
				catch (Exception e)
				{
					Log.Error($"Error al reproducir sonido de cuenta regresiva: {e.Message}");
				}
			}
			if (m_countdown <= 0f)
			{
				Explode();
				m_stateMachine.TransitionTo("Idle");
			}
		}

		private void LeaveCountingDown()
		{
			m_isCountingDown = false;
			m_componentHealth.CorpseDuration = m_originalCorpseDuration;
			if (m_countdownSound != null)
			{
				m_countdownSound.Stop();
				m_countdownSound.Dispose();
				m_countdownSound = null;
			}
		}

		private void Explode()
		{
			if (m_exploded) return;
			m_exploded = true;
			if (m_countdownSound != null)
			{
				m_countdownSound.Stop();
				m_countdownSound.Dispose();
				m_countdownSound = null;
			}
			try
			{
				m_subsystemAudio.PlaySound(ExplosionSoundName, 1f, 0f, m_componentBody.Position, 30f, true);
			}
			catch (Exception e)
			{
				Log.Error($"Error al reproducir sonido de explosión: {e.Message}");
			}
			m_subsystemExplosions.AddExplosion(
				Terrain.ToCell(m_componentBody.Position.X),
				Terrain.ToCell(m_componentBody.Position.Y),
				Terrain.ToCell(m_componentBody.Position.Z),
				ExplosionPressure,
				IsIncendiary,
				false
			);
			m_componentSpawn.Despawn();
		}

		private Vector3 FindNearestPlayerPosition()
		{
			SubsystemPlayers players = Project.FindSubsystem<SubsystemPlayers>(true);
			Vector3 position = m_componentBody.Position;
			float minDist = float.MaxValue;
			Vector3 nearestPos = position;
			foreach (ComponentPlayer player in players.ComponentPlayers)
			{
				if (player != null && player.ComponentHealth.Health > 0f)
				{
					float dist = Vector3.Distance(position, player.ComponentBody.Position);
					if (dist < minDist)
					{
						minDist = dist;
						nearestPos = player.ComponentBody.Position;
					}
				}
			}
			return nearestPos;
		}

		public void Update(float dt)
		{
			if (!m_isCountingDown && m_componentHealth.Health <= 0f && !m_exploded)
			{
				m_stateMachine.TransitionTo("CountingDown");
			}
			m_stateMachine.Update();
		}

		public override void Dispose()
		{
			base.Dispose();
			if (m_countdownSound != null)
			{
				m_countdownSound.Stop();
				m_countdownSound.Dispose();
				m_countdownSound = null;
			}
			if (m_componentHealth != null)
			{
				m_componentHealth.Injured -= OnInjured;
			}
		}
	}
}
