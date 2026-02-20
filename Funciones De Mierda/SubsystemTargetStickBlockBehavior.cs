using System;
using System.Runtime.CompilerServices;
using Engine;

namespace Game
{
	// Token: 0x020000DF RID: 223
	public class SubsystemTargetStickBlockBehavior : SubsystemBlockBehavior
	{
		// Token: 0x170000CA RID: 202
		// (get) Token: 0x06000941 RID: 2369 RVA: 0x0007219C File Offset: 0x0007039C
		public override int[] HandledBlocks
		{
			get
			{
				return new int[]
				{
					BlocksManager.GetBlockIndex(typeof(TargetStickBlock), true, false)
				};
			}
		}

		// Token: 0x06000942 RID: 2370 RVA: 0x000721C8 File Offset: 0x000703C8
		public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
		{
			bool result;
			try
			{
				ComponentPlayer componentPlayer = (componentMiner != null) ? componentMiner.ComponentPlayer : null;
				bool flag = componentPlayer == null;
				if (flag)
				{
					result = false;
				}
				else
				{
					int activeBlockValue = componentMiner.ActiveBlockValue;
					bool flag2 = activeBlockValue == 0;
					if (flag2)
					{
						result = false;
					}
					else
					{
						int num = Terrain.ExtractContents(activeBlockValue);
						int blockIndex = BlocksManager.GetBlockIndex(typeof(TargetStickBlock), true, false);
						bool flag3 = num != blockIndex;
						if (flag3)
						{
							result = false;
						}
						else
						{
							bool flag4 = this.m_subsystemAudio == null;
							if (flag4)
							{
								this.m_subsystemAudio = componentMiner.Project.FindSubsystem<SubsystemAudio>(true);
							}
							bool flag5 = this.m_subsystemCreatureSpawn == null;
							if (flag5)
							{
								this.m_subsystemCreatureSpawn = componentMiner.Project.FindSubsystem<SubsystemCreatureSpawn>(true);
							}
							bool flag6 = this.m_subsystemAudio == null;
							if (flag6)
							{
								result = false;
							}
							else
							{
								ComponentCreatureModel componentCreatureModel = componentMiner.ComponentCreature.ComponentCreatureModel;
								bool flag7 = componentCreatureModel == null;
								if (flag7)
								{
									result = false;
								}
								else
								{
									Vector3 eyePosition = componentCreatureModel.EyePosition;
									Vector3 direction = ray.Direction;
									ComponentCreature componentCreature = null;
									Vector3? vector = null;
									BodyRaycastResult? bodyRaycastResult = componentMiner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, true, null);
									bool flag8 = false;
									bool flag9 = bodyRaycastResult != null && bodyRaycastResult.Value.ComponentBody != null;
									if (flag9)
									{
										ComponentBody componentBody = bodyRaycastResult.Value.ComponentBody;
										componentCreature = componentBody.Entity.FindComponent<ComponentCreature>();
										bool flag10 = componentCreature != null && !this.IsPlayerAlly(componentCreature);
										if (flag10)
										{
											vector = new Vector3?(bodyRaycastResult.Value.HitPoint());
											flag8 = true;
										}
									}
									bool flag11 = !flag8 && this.m_subsystemCreatureSpawn != null;
									if (flag11)
									{
										float num2 = 6f;
										float num3 = 100f;
										ComponentCreature componentCreature2 = null;
										float num4 = 0f;
										foreach (ComponentCreature componentCreature3 in this.m_subsystemCreatureSpawn.Creatures)
										{
											bool flag12 = componentCreature3 == null || componentCreature3 == componentMiner.ComponentCreature;
											if (!flag12)
											{
												bool flag13 = this.IsPlayerAlly(componentCreature3);
												if (!flag13)
												{
													float num5 = Vector3.Distance(componentCreature3.ComponentBody.Position, eyePosition);
													bool flag14 = num5 > num2;
													if (!flag14)
													{
														Vector3 v = Vector3.Normalize(componentCreature3.ComponentBody.Position - eyePosition);
														float num6 = MathUtils.Acos(Vector3.Dot(direction, v)) * 180f / 3.1415927f;
														bool flag15 = num6 > num3;
														if (!flag15)
														{
															float num7 = (num2 - num5) * (num3 - num6);
															bool flag16 = num7 > num4;
															if (flag16)
															{
																num4 = num7;
																componentCreature2 = componentCreature3;
															}
														}
													}
												}
											}
										}
										bool flag17 = componentCreature2 != null;
										if (flag17)
										{
											componentCreature = componentCreature2;
											vector = new Vector3?(componentCreature2.ComponentBody.Position);
											flag8 = true;
										}
									}
									bool flag18 = flag8 && componentCreature != null;
									if (flag18)
									{
										this.m_subsystemAudio.PlaySound("Audio/UI/Attack", 1f, 0f, 0f, 0f);
										bool flag19 = this.m_subsystemCreatureSpawn != null;
										if (flag19)
										{
											Vector3 position = componentMiner.ComponentCreature.ComponentBody.Position;
											float num8 = 30f;
											float maxChaseTime = 45f;
											foreach (ComponentCreature componentCreature4 in this.m_subsystemCreatureSpawn.Creatures)
											{
												bool flag20 = componentCreature4 == null || componentCreature4 == componentMiner.ComponentCreature || componentCreature4 == componentCreature;
												if (!flag20)
												{
													float num9 = Vector3.Distance(componentCreature4.ComponentBody.Position, position);
													bool flag21 = num9 > num8;
													if (!flag21)
													{
														bool flag22 = !this.IsPlayerAlly(componentCreature4);
														if (!flag22)
														{
															ComponentNewHerdBehavior componentNewHerdBehavior = componentCreature4.Entity.FindComponent<ComponentNewHerdBehavior>();
															bool flag23 = componentNewHerdBehavior != null;
															if (flag23)
															{
																bool flag24 = !componentNewHerdBehavior.CanAttackCreature(componentCreature);
																if (flag24)
																{
																	continue;
																}
															}
															else
															{
																ComponentHerdBehavior componentHerdBehavior = componentCreature4.Entity.FindComponent<ComponentHerdBehavior>();
																ComponentHerdBehavior componentHerdBehavior2 = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
																bool flag25 = componentHerdBehavior != null && componentHerdBehavior2 != null && !string.IsNullOrEmpty(componentHerdBehavior.HerdName) && !string.IsNullOrEmpty(componentHerdBehavior2.HerdName);
																if (flag25)
																{
																	bool flag26 = componentHerdBehavior2.HerdName == componentHerdBehavior.HerdName;
																	bool flag27 = false;
																	bool flag28 = componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
																	if (flag28)
																	{
																		bool flag29 = componentHerdBehavior2.HerdName.ToLower().Contains("guardian");
																		if (flag29)
																		{
																			flag27 = true;
																		}
																	}
																	else
																	{
																		bool flag30 = componentHerdBehavior.HerdName.ToLower().Contains("guardian");
																		if (flag30)
																		{
																			bool flag31 = componentHerdBehavior2.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase);
																			if (flag31)
																			{
																				flag27 = true;
																			}
																		}
																	}
																	bool flag32 = flag26 || flag27;
																	if (flag32)
																	{
																		continue;
																	}
																}
															}
															ComponentNewChaseBehavior componentNewChaseBehavior = componentCreature4.Entity.FindComponent<ComponentNewChaseBehavior>();
															ComponentNewChaseBehavior2 componentNewChaseBehavior2 = componentCreature4.Entity.FindComponent<ComponentNewChaseBehavior2>();
															ComponentChaseBehavior componentChaseBehavior = componentCreature4.Entity.FindComponent<ComponentChaseBehavior>();
															bool flag33 = componentNewChaseBehavior != null && !componentNewChaseBehavior.Suppressed;
															if (flag33)
															{
																componentNewChaseBehavior.RespondToCommandImmediately(componentCreature);
															}
															else
															{
																bool flag34 = componentNewChaseBehavior2 != null;
																if (flag34)
																{
																	componentNewChaseBehavior2.RespondToCommandImmediately(componentCreature);
																}
																else
																{
																	bool flag35 = componentChaseBehavior != null;
																	if (flag35)
																	{
																		componentChaseBehavior.Attack(componentCreature, num8, maxChaseTime, false);
																	}
																}
															}
														}
													}
												}
											}
										}
										result = true;
									}
									else
									{
										result = false;
									}
								}
							}
						}
					}
				}
			}
			catch (Exception)
			{
				result = false;
			}
			return result;
		}

		// Token: 0x06000943 RID: 2371 RVA: 0x000727D8 File Offset: 0x000709D8
		private bool IsPlayerAlly(ComponentCreature creature)
		{
			bool flag = creature == null;
			bool result;
			if (flag)
			{
				result = false;
			}
			else
			{
				ComponentNewHerdBehavior componentNewHerdBehavior = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
				bool flag2 = componentNewHerdBehavior != null && !string.IsNullOrEmpty(componentNewHerdBehavior.HerdName);
				if (flag2)
				{
					string herdName = componentNewHerdBehavior.HerdName;
					result = (herdName.Equals("player", StringComparison.OrdinalIgnoreCase) || herdName.ToLower().Contains("guardian"));
				}
				else
				{
					ComponentHerdBehavior componentHerdBehavior = creature.Entity.FindComponent<ComponentHerdBehavior>();
					bool flag3 = componentHerdBehavior != null && !string.IsNullOrEmpty(componentHerdBehavior.HerdName);
					if (flag3)
					{
						string herdName2 = componentHerdBehavior.HerdName;
						result = (herdName2.Equals("player", StringComparison.OrdinalIgnoreCase) || herdName2.ToLower().Contains("guardian"));
					}
					else
					{
						ComponentPlayer componentPlayer = creature.Entity.FindComponent<ComponentPlayer>();
						result = (componentPlayer != null);
					}
				}
			}
			return result;
		}

		// Token: 0x0400077B RID: 1915
		private SubsystemAudio m_subsystemAudio;

		// Token: 0x0400077C RID: 1916
		private SubsystemCreatureSpawn m_subsystemCreatureSpawn;
	}
}
