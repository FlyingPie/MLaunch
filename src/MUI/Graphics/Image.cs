﻿using ImGuiNET;
using System;
using System.Numerics;

namespace MUI.Graphics
{
    public class Image
    {
        public IntPtr Pointer { get; private set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public float Ratio { get; set; }

        public Image()
        { }

        public Image(IntPtr texture, int width, int height)
        {
            if (texture.ToInt32() == 0) throw new ArgumentOutOfRangeException(nameof(texture));

            Pointer = texture;

            Width = width;
            Height = height;

            Ratio = (float)height / (float)width;
        }

        public virtual void Initialize()
        {
        }

        public virtual void Draw(Vector2 size, Vector4 tintColor, Vector4 borderColor, ScaleMode scaleMode = ScaleMode.Fit)
        {
            ImageMath.CalculateScaledImageUV(new Vector2(Width, Height), size, scaleMode, out var uv0, out var uv1);

            ImGui.Image(Pointer, size, uv0, uv1, tintColor, borderColor);
        }
    }
}