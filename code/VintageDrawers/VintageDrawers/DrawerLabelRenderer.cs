using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VintageDrawers
{
    public class DrawerLabelRenderer : IRenderer, IDisposable
    {
        #region Variables
        protected static int TextWidth = 200;
        protected static int TextHeight = 50;
        protected static float QuadWidth = 0.9f;
        protected static float QuadHeight = 0.25f;

        protected CairoFont _font;
        protected BlockPos _pos;
        [NotNull]
        protected ICoreClientAPI _capi;

        protected MeshRef? _textQuadModelRef;
        protected LoadedTexture? _loadedTexture;
        protected MeshRef? _lockIconQuadModelRef;
        protected LoadedTexture? _lockIconTexture;
        
        public Matrixf ModelMat = new Matrixf();

        protected float? _rotX;
        protected float? _rotY;
        protected float? _rotZ;

        protected Vec3f _rotation = Vec3f.Zero;

        protected float? _translateX;
        protected float _translateY = 0.5625f;
        protected float? _translateZ;

        private static readonly float fontSize = 30f;

        public bool _drawText = true;
        public bool _drawLockIcon = true;
        //public bool _drawValue = true;
        public bool ShouldDraw = true;

        private DrawerBE _drawerBE;
        #endregion

        public double RenderOrder => 0.5;
        public int RenderRange => 24;
        public CairoFont Font
        {
            get
            {
                return _font;
            }
            set
            {
                _font = value;
            }
        }

        public DrawerLabelRenderer(DrawerBE p_be, BlockPos p_pos, ICoreClientAPI p_capi)
        {
            _drawerBE = p_be;
            _pos = p_pos;
            _capi = p_capi;
            this._font = new CairoFont((double)DrawerLabelRenderer.fontSize, GuiStyle.StandardFontName, new double[]
                {
                    0.0,
                    0.0,
                    0.0,
                    0.5
                }, null)
            {
                LineHeightMultiplier = 0.8999999761581421
            };
            _capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "DrawerLabelRenderer");
            MeshData quad = QuadMeshUtil.GetQuad();
            quad.Uv = new float[]
            {
                1f,
                1f,
                0f,
                1f,
                0f,
                0f,
                1f,
                0f
            };
            quad.Rgba = new byte[16];
            quad.Rgba.Fill(byte.MaxValue);
            _textQuadModelRef = _capi.Render.UploadMesh(quad);
            MeshData quad2 = QuadMeshUtil.GetQuad();
            quad2.Uv = new float[]
            {
                1f,
                1f,
                0f,
                1f,
                0f,
                0f,
                1f,
                0f
            };
            quad2.Rgba = new byte[16];
            quad2.Rgba.Fill(byte.MaxValue);
            quad2.Scale(new Vec3f(0.5f, 0.5f, 0.5f), 0.15f, 0.45f, 1f);
            this._lockIconQuadModelRef = _capi.Render.UploadMesh(quad2);
            AssetLocation name = new AssetLocation("vintagedrawers:textures/block/lockicon.png");
            _lockIconTexture = new LoadedTexture(_capi);
            _capi.Render.GetOrLoadTexture(name, ref _lockIconTexture);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (_loadedTexture == null || !this.ShouldDraw)
            {
                return;
            }
            if (!_drawLockIcon && !_drawText)
            {
                return;
            }
            Vec3d? cameraPos = _capi.World.Player.Entity.CameraPos;
            if (cameraPos.DistanceTo(_pos.ToVec3d()) > (float)DrawerConfig.Current.LabelInfoMaxRenderDistanceInBlocks)
            {
                if (_drawerBE != null && _drawerBE._shouldDrawMesh)
                {
                    _drawerBE._shouldDrawMesh = false;
                    _drawerBE.MarkDirty(true, null);
                }
                return;
            }
            if (_drawerBE != null && !_drawerBE._shouldDrawMesh)
            {
                _drawerBE._shouldDrawMesh = true;
                _drawerBE.MarkDirty(true, null);
            }
            IRenderAPI render = _capi.Render;
            render.GlDisableCullFace();
            render.GlToggleBlend(true, EnumBlendMode.Standard);
            IStandardShaderProgram standardShaderProgram = render.PreparedStandardShader(_pos.X, _pos.Y, _pos.Z, null);
            float[] values = this.ModelMat.Identity().Translate((double)_pos.X - cameraPos.X, (double)_pos.Y - cameraPos.Y, (double)_pos.Z - cameraPos.Z).Translate(0.5f, 0.5f, 0.5f).Rotate(_rotation).Translate(-0.5, -0.5, -0.5).Translate(0.5f, 0.21f, -0.003f).Scale(0.45f * DrawerLabelRenderer.QuadWidth, 0.4f * DrawerLabelRenderer.QuadHeight, 0.45f * DrawerLabelRenderer.QuadWidth).Values;
            standardShaderProgram.ModelMatrix = values;
            standardShaderProgram.ViewMatrix = render.CameraMatrixOriginf;
            standardShaderProgram.ProjectionMatrix = render.CurrentProjectionMatrix;
            standardShaderProgram.NormalShaded = 0;
            if (_drawText && _loadedTexture != null)
            {
                standardShaderProgram.Tex2D = _loadedTexture.TextureId;
                if (_textQuadModelRef != null)
                {
                    render.RenderMesh(_textQuadModelRef);
                }
            }
            if (_drawLockIcon && _lockIconTexture != null)
            {
                standardShaderProgram.Tex2D = _lockIconTexture.TextureId;
                Mat4f.Translate(values, values, -0.41f, -1.52f, -0.01f);
                standardShaderProgram.ModelMatrix = values;
                if (_lockIconQuadModelRef != null)
                {
                    render.RenderMesh(_lockIconQuadModelRef);
                }
            }            
            standardShaderProgram.Stop();
        }

        public void SetNewTextAndRotation(string text, int color, Vec3f rot)
        {
            _drawText = (text != string.Empty);
            _font?.WithColor(ColorUtil.ToRGBADoubles(color));
            if (_loadedTexture != null)
            {
                _loadedTexture.Dispose();
            }
            _font?.UnscaledFontsize = (double)(DrawerLabelRenderer.fontSize / RuntimeEnv.GUIScale);
            _loadedTexture = _capi?.Gui.TextTexture.GenTextTexture(text, _font, DrawerLabelRenderer.TextWidth, DrawerLabelRenderer.TextHeight, null, EnumTextOrientation.Center, false);
            _rotation = rot;
        }
        public void Dispose()
        {
            this.ShouldDraw = false;
            _capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            if (_loadedTexture != null)
            {
                _loadedTexture.Dispose();
                _loadedTexture = null;
            }
            if (_textQuadModelRef != null)
            {
                _textQuadModelRef.Dispose();
                _textQuadModelRef = null;
            }
            if (_lockIconQuadModelRef != null)
            {
                _lockIconQuadModelRef.Dispose();
                _lockIconQuadModelRef = null;
            }
        }
    }
}
