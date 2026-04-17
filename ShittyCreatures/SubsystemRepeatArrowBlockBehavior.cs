using System;
using Engine;
using Game;
using TemplatesDatabase;

namespace Game
{
	public class SubsystemRepeatArrowBlockBehavior : SubsystemBlockBehavior
	{
		public override int[] HandledBlocks
		{
			get
			{
				return Array.Empty<int>();
			}
		}

		public override void OnFiredAsProjectile(Projectile projectile)
		{
			if (RepeatArrowBlock.GetArrowType(Terrain.ExtractData(projectile.Value)) != RepeatArrowBlock.ArrowType.ExplosiveArrow)
			{
				return;
			}
			this.m_subsystemProjectiles.AddTrail(projectile, Vector3.Zero, new SmokeTrailParticleSystem(20, 0.5f, float.MaxValue, Color.White));
			projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
		}

		public override bool OnHitAsProjectile(CellFace? cellFace, ComponentBody componentBody, WorldItem worldItem)
		{
			RepeatArrowBlock.ArrowType arrowType = RepeatArrowBlock.GetArrowType(Terrain.ExtractData(worldItem.Value));
			float poisonDuration = 0f;
			if (arrowType == RepeatArrowBlock.ArrowType.PoisonArrow)
			{
				poisonDuration = 150f;
			}
			else if (arrowType == RepeatArrowBlock.ArrowType.SeriousPoisonArrow)
			{
				poisonDuration = 300f;
			}
			if (poisonDuration > 0f)
			{
				ComponentPoisonInfected componentPoisonInfected = (componentBody != null) ? componentBody.Entity.FindComponent<ComponentPoisonInfected>() : null;
				ComponentPlayer componentPlayer = ((componentBody != null) ? componentBody.Entity.FindComponent<ComponentCreature>() : null) as ComponentPlayer;
				if (componentPlayer != null)
				{
					if (!componentPlayer.ComponentSickness.IsSick)
					{
						componentPlayer.ComponentSickness.StartSickness();
						componentPlayer.ComponentSickness.m_sicknessDuration = Math.Max(0f, poisonDuration - (componentPoisonInfected != null ? componentPoisonInfected.PoisonResistance : 0f));
					}
				}
				else if (componentPoisonInfected != null)
				{
					componentPoisonInfected.StartInfect(poisonDuration);
				}
			}
			if (worldItem.Velocity.Length() > 10f)
			{
				float breakChance = 0f;
				switch (arrowType)
				{
					case RepeatArrowBlock.ArrowType.CopperArrow:
						breakChance = 0.15f;
						break;
					case RepeatArrowBlock.ArrowType.IronArrow:
						breakChance = 0.075f;
						break;
					case RepeatArrowBlock.ArrowType.DiamondArrow:
						breakChance = 0f;
						break;
					case RepeatArrowBlock.ArrowType.PoisonArrow:
						breakChance = 0.05f;
						break;
					case RepeatArrowBlock.ArrowType.SeriousPoisonArrow:
						breakChance = 0.05f;
						break;
					default:
						breakChance = 0.05f;
						break;
				}
				if (this.m_random.Float(0f, 1f) < breakChance)
				{
					if (arrowType == RepeatArrowBlock.ArrowType.SeriousPoisonArrow)
					{
						worldItem.Value = Terrain.MakeBlockValue(RepeatArrowBlock.Index, 0, RepeatArrowBlock.SetArrowType(0, RepeatArrowBlock.ArrowType.PoisonArrow));
					}
					else if (arrowType == RepeatArrowBlock.ArrowType.PoisonArrow)
					{
						worldItem.Value = Terrain.MakeBlockValue(RepeatArrowBlock.Index, 0, RepeatArrowBlock.SetArrowType(0, RepeatArrowBlock.ArrowType.CopperArrow));
					}
					else if (arrowType != RepeatArrowBlock.ArrowType.ExplosiveArrow)
					{
						return true;
					}
				}
			}
			return false;
		}

		public override void Load(ValuesDictionary valuesDictionary)
		{
			base.Load(valuesDictionary);
			this.m_subsystemProjectiles = base.Project.FindSubsystem<SubsystemProjectiles>(true);
		}

		public SubsystemProjectiles m_subsystemProjectiles;
		public Engine.Random m_random = new Engine.Random();
	}
}