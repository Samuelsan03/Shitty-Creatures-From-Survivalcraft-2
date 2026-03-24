using System;
using System.IO;
using Engine;
using Engine.Graphics;
using Engine.Media;

namespace Game
{
	public static class ShittyCreaturesBlockManager
	{
		static ShittyCreaturesBlockManager()
		{
			Storage.CreateDirectory(TexturesDirectoryName);
			DefaultShittyCreaturesTexture = ContentManager.Get<Texture2D>("Textures/ShittyCreaturesTextures");
		}

		public static Texture2D DefaultShittyCreaturesTexture { get; set; }

		public static string TexturesDirectoryName => "Textures/ShittyCreaturesTextures";

		public static event Action<string> ShittyCreaturesTextureDeleted;

		public static bool IsBuiltIn(string name)
		{
			return string.IsNullOrEmpty(name);
		}

		public static string GetFileName(string name)
		{
			if (IsBuiltIn(name))
				return null;
			return Storage.CombinePaths(TexturesDirectoryName, name);
		}

		public static string GetDisplayName(string name)
		{
			if (IsBuiltIn(name))
				return LanguageControl.Get(new[] { "Usual", "gameName" });
			return Storage.GetFileNameWithoutExtension(name);
		}

		public static DateTime GetCreationDate(string name)
		{
			try
			{
				if (!IsBuiltIn(name))
					return Storage.GetFileLastWriteTime(GetFileName(name));
			}
			catch { }
			return new DateTime(2000, 1, 1);
		}

		public static Texture2D LoadTexture(string name)
		{
			Texture2D texture = null;
			if (!IsBuiltIn(name))
			{
				try
				{
					string fileName = GetFileName(name);
					if (Storage.FileExists(fileName))
					{
						string ext = Storage.GetExtension(fileName.Replace(".scbtex", "")).ToLower();
						if (ext == ".astc" || ext == ".astcsrgb")
						{
							using (Stream stream = Storage.OpenFile(fileName, OpenFileMode.Read))
							{
								if (CompressedTexture2D.GetParameters(stream, out int width, out int height, out _, out _))
								{
									ValidateTextureSize(width, height);
									texture = CompressedTexture2D.Load(stream, true, 1);
								}
							}
						}
						else
						{
							Engine.Media.Image image = Engine.Media.Image.Load(fileName);
							ValidateTextureSize(image);
							texture = Texture2D.Load(image, 1);
							texture.Tag = image;
						}
					}
					else
					{
						Log.Warning($"ShittyCreatures texture '{name}' not found.");
					}
				}
				catch (Exception ex)
				{
					Log.Warning($"Failed to load ShittyCreatures texture '{name}': {ex.Message}");
				}
			}
			return texture ?? DefaultShittyCreaturesTexture;
		}

		public static string ImportShittyCreaturesTexture(string name, Stream stream)
		{
			var ex = ExternalContentManager.VerifyExternalContentName(name);
			if (ex != null)
				throw ex;

			string ext = Storage.GetExtension(name).ToLower();
			if (ext != ".scbtex")
				name += ".scbtex";

			if (ext == ".astc" || ext == ".astcsrgb")
			{
				if (!CompressedTexture2D.GetParameters(stream, out int width, out int height, out _, out _))
					throw new InvalidOperationException("Invalid ASTC file.");
				ValidateTextureSize(width, height);
			}
			else
			{
				ValidateTextureSize(stream);
			}

			stream.Position = 0;
			using (Stream dest = Storage.OpenFile(GetFileName(name), OpenFileMode.Create))
			{
				stream.CopyTo(dest);
			}
			return name;
		}

		public static void DeleteShittyCreaturesTexture(string name)
		{
			try
			{
				string fileName = GetFileName(name);
				if (!string.IsNullOrEmpty(fileName))
				{
					Storage.DeleteFile(fileName);
					ShittyCreaturesTextureDeleted?.Invoke(name);
				}
			}
			catch (Exception e)
			{
				ExceptionManager.ReportExceptionToUser($"Failed to delete creature texture '{name}'", e);
			}
		}

		public static void UpdateShittyCreaturesTexturesList()
		{
			// Similar to UpdateBlocksTexturesList if needed
		}

		private static void ValidateTextureSize(Stream stream)
		{
			Engine.Media.Image image = Engine.Media.Image.Load(stream);
			ValidateTextureSize(image);
		}

		private static void ValidateTextureSize(Engine.Media.Image image)
		{
			ValidateTextureSize(image.Width, image.Height);
		}

		private static void ValidateTextureSize(int width, int height)
		{
			if (width > 65536 || height > 65536)
				throw new InvalidOperationException($"Texture too large: {width}x{height}");
			if (!MathUtils.IsPowerOf2(width) || !MathUtils.IsPowerOf2(height))
				throw new InvalidOperationException($"Texture dimensions must be power of two: {width}x{height}");
		}
	}
}
