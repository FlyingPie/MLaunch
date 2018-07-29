﻿using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using MUI.Graphics;
using System;
using System.Collections.Concurrent;
using System.IO;

namespace MUI
{
    public class ResourceManager
    {
        public Image DefaultImage { get; private set; }

        private ConcurrentDictionary<string, Image> _loadedImages;
        private TextureLoader _textureLoader;

        private GraphicsDevice _graphicsDevice;
        private IImGuiRenderer _imGuiRenderer;

        public ResourceManager(GraphicsDevice graphicsDevice, IImGuiRenderer imGuiRenderer)
        {
            _loadedImages = new ConcurrentDictionary<string, Image>();
            _textureLoader = new TextureLoader(graphicsDevice, imGuiRenderer);

            _graphicsDevice = graphicsDevice;
            _imGuiRenderer = imGuiRenderer;
        }

        public void Init()
        {
            // TODO: Move this
            DefaultImage = LoadImage(@"Resources\Images\crashlog-doom.png");
        }

        public Font LoadFont(string path, int pixelSize)
        {
            var font = ImGui.GetIO().FontAtlas.AddFontFromFileTTF(path, pixelSize);

            _imGuiRenderer.RebuildFontAtlas();

            return font;
        }

        // TODO: Add size cap
        public Image LoadImage(string path)
        {
            return LoadImage(path, File.ReadAllBytes(path));
        }

        // TODO: Add size cap
        public Image LoadImage(string cacheKey, byte[] data)
        {
            return LoadImage(cacheKey, factory => factory.Load(data));
        }

        public Image LoadImage(string cacheKey, Func<TextureLoader, Image> factory)
        {
            return _loadedImages.GetOrAdd(cacheKey, key => factory(_textureLoader));
        }
    }
}