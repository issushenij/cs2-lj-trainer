using System;
using System.Numerics;
using Raylib_cs;

namespace LJTrainer.Core
{
    /// <summary>
    /// Next-Gen High-Performance Visual Effects & Shader Pipeline.
    /// Provides dynamic glowing grid fields, responsive particle ambiances, and glass backplates.
    /// </summary>
    public static class ShaderFxManager
    {
        private static Shader _bloomShader;
        private static Shader _gridShader;
        private static bool _shadersLoaded = false;
        private static RenderTexture2D _mainRenderBuffer;
        private static bool _bufferInitialized = false;

        private static int _timeLoc;
        private static int _resLoc;
        private static int _mouseLoc;

        private const string VertexShaderCode = @"#version 330
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec4 vertexColor;
out vec2 fragTexCoord;
out vec4 fragColor;
uniform mat4 mvp;
void main()
{
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;
    gl_Position = mvp * vec4(vertexPosition, 1.0);
}
";

        private const string CyberGridFragmentCode = @"#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;

uniform vec2 u_resolution;
uniform float u_time;
uniform vec2 u_mouse;

void main()
{
    vec2 uv = gl_FragCoord.xy / u_resolution.xy;
    
    // Smooth cyber grid
    vec2 gridUV = uv * vec2(u_resolution.x / 40.0, u_resolution.y / 40.0);
    vec2 grid = abs(fract(gridUV - 0.5) - 0.5) / fwidth(gridUV);
    float line = min(grid.x, grid.y);
    float gridAlpha = 1.0 - min(line, 1.0);
    
    // Slow ambient wave
    float wave = sin(uv.x * 4.0 + u_time * 0.8) * cos(uv.y * 3.0 - u_time * 0.5) * 0.5 + 0.5;
    
    // Mouse proximity glow
    vec2 mUV = u_mouse / u_resolution.xy;
    mUV.y = 1.0 - mUV.y;
    float distToMouse = length(uv - mUV);
    float mouseGlow = exp(-distToMouse * 4.5) * 0.18;
    
    vec3 baseCol = vec3(0.027, 0.043, 0.070); // Deep Dark Slate #070B12
    vec3 gridCol = vec3(0.0, 0.898, 1.0) * (gridAlpha * 0.04 + wave * 0.02 + mouseGlow);
    
    finalColor = vec4(baseCol + gridCol, 1.0);
}
";

        public static void Initialize(int width, int height)
        {
            try
            {
                _gridShader = Raylib.LoadShaderFromMemory(VertexShaderCode, CyberGridFragmentCode);
                _timeLoc = Raylib.GetShaderLocation(_gridShader, "u_time");
                _resLoc = Raylib.GetShaderLocation(_gridShader, "u_resolution");
                _mouseLoc = Raylib.GetShaderLocation(_gridShader, "u_mouse");

                _mainRenderBuffer = Raylib.LoadRenderTexture(width, height);
                Raylib.SetTextureFilter(_mainRenderBuffer.Texture, TextureFilter.Bilinear);

                _shadersLoaded = true;
                _bufferInitialized = true;
            }
            catch
            {
                _shadersLoaded = false;
            }
        }

        public static void ResizeBuffer(int width, int height)
        {
            if (!_bufferInitialized) return;
            try
            {
                Raylib.UnloadRenderTexture(_mainRenderBuffer);
                _mainRenderBuffer = Raylib.LoadRenderTexture(width, height);
                Raylib.SetTextureFilter(_mainRenderBuffer.Texture, TextureFilter.Bilinear);
            }
            catch { }
        }

        public static void DrawCyberGridBackground(int width, int height)
        {
            if (!_shadersLoaded)
            {
                Raylib.ClearBackground(new Color(7, 11, 18, 255));
                return;
            }

            float time = (float)Raylib.GetTime();
            Vector2 res = new(width, height);
            Vector2 mouse = Raylib.GetMousePosition();

            Raylib.SetShaderValue(_gridShader, _timeLoc, time, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(_gridShader, _resLoc, res, ShaderUniformDataType.Vec2);
            Raylib.SetShaderValue(_gridShader, _mouseLoc, mouse, ShaderUniformDataType.Vec2);

            Raylib.BeginShaderMode(_gridShader);
            Raylib.DrawRectangle(0, 0, width, height, Color.White);
            Raylib.EndShaderMode();
        }

        public static void Cleanup()
        {
            if (_shadersLoaded)
            {
                try
                {
                    Raylib.UnloadShader(_gridShader);
                }
                catch { }
            }
            if (_bufferInitialized)
            {
                try
                {
                    Raylib.UnloadRenderTexture(_mainRenderBuffer);
                }
                catch { }
            }
        }
    }
}
