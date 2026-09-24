using System;
using System.Collections.Generic;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class ComponentRandomChatter : Component, IUpdateable
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
			"Hola chamitos, aquí el karajito que le sigue la corriente a moralistas",
			"Tengo hambre we",
			"Chupame la pija",
			"Me empezo a meterme a sus grupos de mierda",
			"Los visitare en la noche",
			"Ay ese benson",
			"Callate la boca pendejo",
			"El problema es que somos demasiados",
			"Ya largate de este directo",
			"Te dome sin condon",
			"La bebecita bebe lean y bebe whisky",
			"Soy guapo, lo sé\r\nLas mujeres se calientan, ya lo sé",
			"Dejenmen en paz!",
			"Aw shit, here we go again",
			"Come on sweetheart",
			"La marihuana para siempre",
			"I shot the sheriff",
			"Estoy en tu cesped Nebbercracker xdxdxdxd",
			"¿Dónde están los que hablan de mí?\r\n¿Dónde están? Por el techo van a salir\r\n¿Dónde están los que hablan de mí?\r\n¿Dónde están? Por el techo van a salir",
			"La quimica no fisica magnifica lirica mistica\r\nla habilidad lenguistica y calidad olimpica\r\nhara que esa nena bella baile\r\nen la casa cuando wiso cante",
			"Ohh yes,\r\nElla es mi chica de la voz sensual\r\nUna romántica llamada\r\nQue penetra en mi corazón y me hace enamorar.\r\nCon forme con forme... one more time"
		};
	}
}
