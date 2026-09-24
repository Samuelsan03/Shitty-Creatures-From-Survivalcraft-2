using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentRandomChatter2 : Component, IUpdateable
	{
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		public override void Load(ValuesDictionary values, IdToEntityMap map)
		{
			this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
			this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
			this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
			this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
			this.m_componentHealth = base.Entity.FindComponent<ComponentHealth>();
			this.SetNextChatterTime();
		}

		public void Update(float dt)
		{
			if (this.m_componentHealth != null && this.m_componentHealth.Health <= 0f)
				return;

			if (this.m_subsystemTime.GameTime < this.m_nextChatterTime)
				return;

			bool playerNear = false;
			foreach (PlayerData playerData in this.m_subsystemPlayers.PlayersData)
			{
				ComponentPlayer componentPlayer = playerData.ComponentPlayer;
				if (((componentPlayer != null) ? componentPlayer.ComponentBody : null) != null &&
					Vector3.DistanceSquared(this.m_componentBody.Position, playerData.ComponentPlayer.ComponentBody.Position) < 25f)
				{
					playerNear = true;
					break;
				}
			}
			if (!playerNear)
				return;

			// CORREGIDO: Se quita el 'this.' porque es miembro estático
			string phrase = m_phrases[this.m_random.Int(0, m_phrases.Count - 1)];
			Vector3 pos = (this.m_componentBody.BoundingBox.Min + this.m_componentBody.BoundingBox.Max) * 0.5f;
			pos.Y = this.m_componentBody.BoundingBox.Max.Y;
			this.m_subsystemParticles.AddParticleSystem(new ChatterParticleSystem(pos, Color.White, phrase, 2.5f));
			this.SetNextChatterTime();
		}

		private void SetNextChatterTime()
		{
			this.m_nextChatterTime = this.m_subsystemTime.GameTime + 30.0;
		}

		private SubsystemTime m_subsystemTime;
		private SubsystemPlayers m_subsystemPlayers;
		private SubsystemParticles m_subsystemParticles;
		private ComponentBody m_componentBody;
		private ComponentHealth m_componentHealth;
		private Game.Random m_random = new Game.Random();
		private double m_nextChatterTime;

		private static readonly List<string> m_phrases = new List<string>
		{
			"Alianza es una mierda\r\nMerece ser exterminada",
			"A la mierda Alianza",
			"Los Digimons merecen ser exterminados",
			"Viva Muerte\r\nAbajo Alianza",
			"Oh yeah, fuck, I'm coming!",
			"Yeah Baby",
			"Viva el señor Mencho",
			"El problema es que somos demasiados",
			"Muerte ya esta aquí",
			"El infierno morado es el mejor paraíso",
			"Te vamos a follar zorra",
			"Somos tus machos a joderte la vida she",
			"Fuck, esto es lo mejor",
			"Oh yeah baby",
			"Chinga tu madre, motherfucker",
			"Son a bitch",
			"Yo te boté\r\nTe di banda y te solté, yo te solté\r\nPa'l carajo te mandé, yo te mandé\r\nY a tu amiga me clavé, me la clavé\r\nFuck you, hijueputa, yeh",
			"Vete a la VRG",
			"Criminal, cri-criminal\r\nTu estilo, tu flow, mami, muy criminal"
		};
	}
}
