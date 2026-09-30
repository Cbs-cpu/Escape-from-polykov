#!/usr/bin/env python3
"""Generates Assets/_Project/Scenes/Lobby.unity (camera, sun, LobbyController). Re-run to regenerate."""
import pathlib
root = pathlib.Path(__file__).resolve().parents[1]
arena = (root / "Assets/_Project/Scenes/MovementTestArena.unity").read_text()
header = arena[:arena.index("--- !u!1 &")]
# Linear fog in the background colour: the lobby floor fades into the void instead of ending at a hard horizon.
for old, new in (("m_Fog: 0", "m_Fog: 1"),
                 ("m_FogColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}", "m_FogColor: {r: 0.035, g: 0.038, b: 0.04, a: 1}"),
                 ("m_FogMode: 3", "m_FogMode: 1"),
                 ("m_LinearFogStart: 0", "m_LinearFogStart: 10"),
                 ("m_LinearFogEnd: 300", "m_LinearFogEnd: 20")):
    assert old in header, old
    header = header.replace(old, new)
body = """--- !u!1 &100
GameObject:
  m_ObjectHideFlags: 0
  serializedVersion: 6
  m_Component:
  - component: {fileID: 101}
  - component: {fileID: 102}
  - component: {fileID: 103}
  m_Layer: 0
  m_Name: Main Camera
  m_TagString: MainCamera
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &101
Transform:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 100}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 1.32, z: -3.55}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProximity: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!20 &102
Camera:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 100}
  m_Enabled: 1
  serializedVersion: 2
  m_ClearFlags: 2
  m_BackGroundColor: {r: 0.06, g: 0.065, b: 0.07, a: 0}
  m_projectionMatrixMode: 1
  m_GateFitMode: 2
  m_FOVAxisMode: 0
  m_Iso: 200
  m_ShutterSpeed: 0.005
  m_Aperture: 16
  m_FocusDistance: 10
  m_FocalLength: 50
  m_BladeCount: 5
  m_Curvature: {x: 2, y: 11}
  m_BarrelClipping: 0.25
  m_Anamorphism: 0
  m_SensorSize: {x: 36, y: 24}
  m_LensShift: {x: 0, y: 0}
  m_NormalizedViewPortRect:
    serializedVersion: 2
    x: 0
    y: 0
    width: 1
    height: 1
  near clip plane: 0.05
  far clip plane: 200
  field of view: 30
  orthographic: 0
  orthographic size: 5
  m_Depth: 0
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingPath: -1
  m_TargetTexture: {fileID: 0}
  m_TargetDisplay: 0
  m_TargetEye: 3
  m_HDR: 1
  m_AllowMSAA: 1
  m_AllowDynamicResolution: 0
  m_ForceIntoRT: 0
  m_OcclusionCulling: 1
  m_StereoConvergence: 10
  m_StereoSeparation: 0.022
--- !u!81 &103
AudioListener:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 100}
  m_Enabled: 1
--- !u!1 &200
GameObject:
  m_ObjectHideFlags: 0
  serializedVersion: 6
  m_Component:
  - component: {fileID: 201}
  - component: {fileID: 202}
  - component: {fileID: 203}
  m_Layer: 0
  m_Name: Directional Light
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &201
Transform:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 200}
  serializedVersion: 2
  m_LocalRotation: {x: 0.40821788, y: -0.23456968, z: 0.10938163, w: 0.87542605}
  m_LocalPosition: {x: 0, y: 3, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProximity: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 50, y: -30, z: 0}
--- !u!108 &202
Light:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 200}
  m_Enabled: 1
  serializedVersion: 10
  m_Type: 1
  m_Color: {r: 1, g: 0.96, b: 0.9, a: 1}
  m_Intensity: 0.6
  m_Range: 10
  m_SpotAngle: 30
  m_InnerSpotAngle: 21.80208
  m_CookieSize: 10
  m_Shadows:
    m_Type: 0
    m_Resolution: -1
    m_CustomResolution: -1
    m_Strength: 1
    m_Bias: 0.05
    m_NormalBias: 0.4
    m_NearPlane: 0.2
    m_CullingMatrixOverride:
      e00: 1
      e01: 0
      e02: 0
      e03: 0
      e10: 0
      e11: 1
      e12: 0
      e13: 0
      e20: 0
      e21: 0
      e22: 1
      e23: 0
      e30: 0
      e31: 0
      e32: 0
      e33: 1
    m_UseCullingMatrixOverride: 0
  m_Cookie: {fileID: 0}
  m_DrawHalo: 0
  m_Flare: {fileID: 0}
  m_RenderMode: 0
  m_CullingMask:
    serializedVersion: 2
    m_Bits: 4294967295
  m_RenderingLayerMask: 1
  m_Lightmapping: 4
  m_LightShadowCasterMode: 0
  m_AreaSize: {x: 1, y: 1}
  m_BounceIntensity: 1
  m_ColorTemperature: 6570
  m_UseColorTemperature: 0
  m_BoundingSphereOverride: {x: 0, y: 0, z: 0, w: 0}
  m_UseBoundingSphereOverride: 0
  m_UseViewFrustumForShadowCasterCull: 1
  m_ShadowRadius: 0
  m_ShadowAngle: 0
--- !u!114 &203
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 200}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 474bcb49853aa07438625e644c072ee6, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
--- !u!1 &300
GameObject:
  m_ObjectHideFlags: 0
  serializedVersion: 6
  m_Component:
  - component: {fileID: 301}
  - component: {fileID: 302}
  m_Layer: 0
  m_Name: Lobby
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &301
Transform:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 300}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProximity: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &302
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 300}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 3b95e253da5b4a308dd6e8f4c28fe5b4, type: 3}
  m_Name: 
  m_EditorClassIdentifier: Polykov.Lobby::Polykov.Lobby.LobbyController
  definition: {fileID: 11400000, guid: 982d6e938cdf451ba5c842df53189bed, type: 2}
  weaponModelPrefab: {fileID: 919132149155446097, guid: e9be28d5ae53bd6429e56dfe4893a1bf, type: 3}
  weaponMaterial: {fileID: 2100000, guid: 5e33ba6a3ff97ea49a967d2ccda621b2, type: 2}
  outlineMaterial: {fileID: 2100000, guid: 4b8b05f17c45ce84382d0a36d6524186, type: 2}
  characterModel: {fileID: 919132149155446097, guid: 2cef109dc5c60fb4ca34168a9182a1f8, type: 3}
  characterController: {fileID: 9100000, guid: b99d2f4381d74314f8c0c74f40bb2ff3, type: 2}
  characterMaterial: {fileID: 2100000, guid: 05ea86d4ea105c845bd40cf667e65c6a, type: 2}
  gameScene: MovementTestArena
  stageCamera: {fileID: 102}
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {fileID: 101}
  - {fileID: 201}
  - {fileID: 301}
"""
out = root / "Assets/_Project/Scenes/Lobby.unity"
out.write_text(header + body)
print("wrote", out)
