// LargeCraftingRecipesManager.cs - Solo la parte del path corregida

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Engine;
using XmlUtilities;

namespace Game
{
	public class LargeCraftingRecipe
	{
		public int ResultValue;
		public int ResultCount;
		public int RemainsValue;
		public int RemainsCount;
		public string[] Ingredients = new string[25]; // 5x5
		public string Description;
		public int DisplayOrder;
	}

	public static class LargeCraftingRecipesManager
	{
		private static List<LargeCraftingRecipe> m_recipes = new List<LargeCraftingRecipe>();

		public static event Action<List<LargeCraftingRecipe>> AddRecipes;

		public static IReadOnlyList<LargeCraftingRecipe> Recipes => m_recipes;

		public static void Initialize()
		{
			m_recipes.Clear();

			// Cargar recetas desde el archivo usando ContentManager
			try
			{
				// Buscar el archivo LargeCraftingRecipes.xml en los recursos del mod
				XElement root = ContentManager.Get<XElement>("LargeCraftingRecipes", "xml", false);
				if (root != null)
				{
					LoadRecipes(root);
				}
				else
				{
					Log.Warning("LargeCraftingRecipes.xml not found in mod resources.");
				}
			}
			catch (Exception e)
			{
				Log.Error("Error loading LargeCraftingRecipes.xml: " + e.Message);
			}

			// Disparar evento para otros mods
			if (AddRecipes != null)
			{
				var tempList = new List<LargeCraftingRecipe>();
				AddRecipes(tempList);
				m_recipes.AddRange(tempList);
			}

			// Ordenar por DisplayOrder
			m_recipes.Sort((r1, r2) => r1.DisplayOrder.CompareTo(r2.DisplayOrder));
		}

		private static void LoadRecipes(XElement element)
		{
			foreach (XElement child in element.Elements())
			{
				if (child.Attribute("Result") == null)
				{
					LoadRecipes(child);
				}
				else
				{
					LargeCraftingRecipe recipe = DecodeElementToCraftingRecipe(child, 5);
					m_recipes.Add(recipe);
				}
			}
		}

		private static LargeCraftingRecipe DecodeElementToCraftingRecipe(XElement element, int size)
		{
			LargeCraftingRecipe recipe = new LargeCraftingRecipe();

			string resultStr = XmlUtils.GetAttributeValue<string>(element, "Result");
			recipe.ResultValue = DecodeResult(resultStr);
			recipe.ResultCount = XmlUtils.GetAttributeValue<int>(element, "ResultCount");

			string remainsStr = XmlUtils.GetAttributeValue<string>(element, "Remains", null);
			if (!string.IsNullOrEmpty(remainsStr))
			{
				recipe.RemainsValue = DecodeResult(remainsStr);
				recipe.RemainsCount = XmlUtils.GetAttributeValue<int>(element, "RemainsCount");
			}

			recipe.Description = XmlUtils.GetAttributeValue<string>(element, "Description", "");
			recipe.DisplayOrder = XmlUtils.GetAttributeValue<int>(element, "DisplayOrder", 0);

			Dictionary<char, string> charMap = new Dictionary<char, string>();
			foreach (XAttribute attr in element.Attributes())
			{
				if (attr.Name.LocalName.Length == 1 && char.IsLower(attr.Name.LocalName[0]))
				{
					charMap[attr.Name.LocalName[0]] = attr.Value;
				}
			}

			string[] lines = element.Value.Trim().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
			int expectedLines = size;
			if (lines.Length != expectedLines)
				throw new InvalidOperationException($"La receta debe tener {expectedLines} líneas, pero tiene {lines.Length}.");

			for (int y = 0; y < expectedLines; y++)
			{
				string line = lines[y].Trim();
				int start = line.IndexOf('"');
				int end = line.LastIndexOf('"');
				if (start < 0 || end < 0 || end <= start)
					throw new InvalidOperationException("Línea de receta mal formada.");

				string pattern = line.Substring(start + 1, end - start - 1);
				if (pattern.Length != size)
					throw new InvalidOperationException($"Cada línea debe tener {size} caracteres.");

				for (int x = 0; x < size; x++)
				{
					char c = pattern[x];
					if (char.IsLower(c))
					{
						if (!charMap.ContainsKey(c))
							throw new InvalidOperationException($"Carácter '{c}' no definido en los atributos.");
						recipe.Ingredients[x + y * size] = charMap[c];
					}
					else
					{
						recipe.Ingredients[x + y * size] = null;
					}
				}
			}

			return recipe;
		}

		private static int DecodeResult(string result)
		{
			string[] parts = result.Split(':');
			string blockName = parts[0];
			int data = parts.Length == 2 ? int.Parse(parts[1]) : 0;
			return Terrain.MakeBlockValue(BlocksManager.GetBlockIndex(blockName, true), 0, data);
		}

		public static LargeCraftingRecipe FindMatchingRecipe(SubsystemTerrain terrain, string[] ingredients, float heatLevel, float playerLevel)
		{
			if (ingredients.All(string.IsNullOrEmpty))
				return null;

			foreach (var recipe in m_recipes)
			{
				if (MatchRecipe(recipe.Ingredients, ingredients))
				{
					return recipe;
				}
			}
			return null;
		}

		private static bool MatchRecipe(string[] required, string[] actual)
		{
			int size = 5;
			if (actual.Length != size * size)
				return false;

			for (int i = 0; i < size * size; i++)
			{
				if (!string.Equals(required[i], actual[i]))
					return false;
			}
			return true;
		}

		public static void AddRecipe(LargeCraftingRecipe recipe)
		{
			if (recipe != null)
				m_recipes.Add(recipe);
		}
	}
}
