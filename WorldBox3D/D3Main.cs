﻿using System.Collections.Generic;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
//using System.Drawing;
using System.IO;
using HarmonyLib;
using System.Reflection;
using DG.Tweening;
using UnityEngine.Tilemaps;
using System;
using System.Linq;
//using TextureLoader;

namespace WorldBox3D {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [System.Obsolete]
    class _3D_Main : BaseUnityPlugin {
        public static Dictionary<string, int> buildingCustomHeight = new Dictionary<string, int>();
        public static Dictionary<string, int> buildingCustomThickness = new Dictionary<string, int>();
        public static Dictionary<string, int> buildingCustomAngle = new Dictionary<string, int>();

        public static LightManager lightMan = new LightManager();
        public void SettingSetup()
        {
            regularThickeningScale = Config.AddSetting("3D - Scaling", "Regular Thickness", 7, "How many extra layers the buildings get");
            regularThickeningCityMultiplier = Config.AddSetting("3D - Scaling", "City Multiplier", 2, "Multiplier for town buildings");
        }
        public static ConfigEntry<int> regularThickeningScale {
            get; set;
        }
        public static ConfigEntry<int> regularThickeningCityMultiplier {
            get; set;
        }
        public const string pluginGuid = "cody.worldbox.3d";
        public const string pluginName = "WorldBox3D";
        public const string pluginVersion = "0.0.0.3";
        public float rotationRate = 2f;
        public float manipulationRate = 0.01f;
        public float cameraX => Camera.main.transform.rotation.x;
        public float cameraY => Camera.main.transform.rotation.y;
        public float cameraZ;
        public Transform cameraTransform => Camera.main.transform;
        public List<LineRenderer> activeLines = new List<LineRenderer>();
        bool firstRun;
        static bool finishedLoading;
        public bool autoPlacement;
        public static bool _3dEnabled = false;


        public Vector3 RandomCircle(Vector3 center, float radius, int a)
        {
            Debug.Log(a);
            float ang = a;
            Vector3 pos;
            pos.x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad);
            pos.y = center.y + radius * Mathf.Cos(ang * Mathf.Deg2Rad);
            pos.z = center.z;
            return pos;
        }

        public void SpawnCloudsInCircle(WorldTile centerTile, int count)
        {
            Vector3 center = centerTile.posV3; //transform.position;
            for(int i = 0; i < count; i++) {
                int a = i * 30;
                Vector3 pos = RandomCircle(center, 30f, a);
                Cloud cloud = MapBox.instance.cloudController.getNext();
                cloud.setScale(Toolbox.randomFloat(0f, 0.1f));
                cloud.tile = MapBox.instance.GetTile((int)center.x, (int)center.y);
                cloud.transform.localPosition = new Vector3(pos.x, pos.y, -Toolbox.randomFloat(1f, 10f));
                hurricaneList.Add(cloud);
                // Instantiate(prefab, pos, Quaternion.identity);
            }
        }

        public static int dividerAmount = 10;

        // tile3d setup
        public static bool setTile_Prefix(WorldTile pWorldTile, Vector3Int pVec, Tile pGraphic, TilemapExtended __instance)
        {
            if(tile3Denabled) {
                List<Vector3Int> vec = Reflection.GetField(__instance.GetType(), __instance, "vec") as List<Vector3Int>;
                List<Tile> tiles = Reflection.GetField(__instance.GetType(), __instance, "tiles") as List<Tile>;
                Tile curGraphics = Reflection.GetField(pWorldTile.GetType(), pWorldTile, "curGraphics") as Tile;

                pVec.z = (-pWorldTile.Height) / dividerAmount; // main change, everything else is recreation/replacement

                if(curGraphics == pGraphic && pGraphic != null) {
                    return false;
                }
                curGraphics = pGraphic;
                vec.Add(pVec);
                tiles.Add(pGraphic);
                return false;
            }
            return true;
        }

        public static bool tile3Denabled;
        public static List<Cloud> hurricaneList = new List<Cloud>();
        public static void update_CloudPostfix(float pElapsed, Cloud __instance)
        {
            if(_3dEnabled) {
                __instance.transform.position = new Vector3(__instance.transform.position.x, __instance.transform.position.y, -20f);
            }
            if(hurricaneList.Contains(__instance)) {
                __instance.transform.RotateAround(__instance.tile.posV3, Vector3.forward, 20 * Time.deltaTime * Toolbox.randomFloat(0f, 5f));
            }
            else {
                __instance.transform.Translate(-(1f * pElapsed), 0f, 0f);
            }
        }

        public bool tryMesh;
        public List<Actor> meshdActors = new List<Actor>();
        public List<Building> meshdBuildings = new List<Building>();

        public List<meshT> meshes = new List<meshT>();
        public class meshT {
            public Mesh mesh;
            public Material mat;
            public Actor Actor;
        }

        public Mesh spriteToMesh(Sprite sprite, int depth)
        {
            Mesh mesh = new Mesh();
            List<Vector3> inVertices = Array.ConvertAll(sprite.vertices, i => (Vector3)i).ToList();
            List<Vector2> uvs = sprite.uv.ToList();
            List<int> triangles = Array.ConvertAll(sprite.triangles, i => (int)i).ToList();
            if(depth <= 0) {
                mesh.SetVertices(inVertices);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
            }
            else {
                List<Vector3> depthVerticles = new List<Vector3>(inVertices);
                List<Vector2> depthUvs = new List<Vector2>(uvs);
                List<int> depthTriangles = new List<int>(triangles);
                foreach(Vector3 vector in inVertices) {
                    depthVerticles.Add(new Vector3(vector.x, vector.y, depth));
                }
                foreach(Vector2 vector in uvs) {
                    depthUvs.Add(new Vector2(vector.x, vector.y));
                }
                //depthTriangles.Add(triangles[0] + inVertices.Count);
                for(int i = triangles.Count - 1; i >= 0; i--) {
                    depthTriangles.Add((triangles[i] + inVertices.Count));
                }
                List<int> depthTrianglesWithJoinFaces = new List<int>(depthTriangles);
                for(int i = 0; i + 1 < triangles.Count; i++) {
                    depthTrianglesWithJoinFaces.Add(triangles[i + 1]);
                    depthTrianglesWithJoinFaces.Add(triangles[i]);
                    depthTrianglesWithJoinFaces.Add(triangles[i + 1] + inVertices.Count);
                    depthTrianglesWithJoinFaces.Add(triangles[i + 1] + inVertices.Count);
                    depthTrianglesWithJoinFaces.Add(triangles[i]);
                    depthTrianglesWithJoinFaces.Add(triangles[i] + inVertices.Count);
                }
                for(int i = 0; i + 2 + inVertices.Count < depthVerticles.Count; i++) {
                    depthTrianglesWithJoinFaces.Add(i);
                    depthTrianglesWithJoinFaces.Add(i + 2);
                    depthTrianglesWithJoinFaces.Add(i + 2 + inVertices.Count);
                    depthTrianglesWithJoinFaces.Add(i);
                    depthTrianglesWithJoinFaces.Add(i + 2 + inVertices.Count);
                    depthTrianglesWithJoinFaces.Add(i + inVertices.Count);
                }
                for(int i = 0; i + 1 + inVertices.Count < depthVerticles.Count; i++) {
                    depthTrianglesWithJoinFaces.Add(i);
                    depthTrianglesWithJoinFaces.Add(i + 1);
                    depthTrianglesWithJoinFaces.Add(i + 1 + inVertices.Count);
                    depthTrianglesWithJoinFaces.Add(i);
                    depthTrianglesWithJoinFaces.Add(i + 1 + inVertices.Count);
                    depthTrianglesWithJoinFaces.Add(i + inVertices.Count);
                }
                mesh.SetVertices(depthVerticles.ToList());
                mesh.SetUVs(0, depthUvs);
                mesh.SetTriangles(depthTrianglesWithJoinFaces, 0);
                string values = "";
                int j = 0;
                foreach(int i in depthTrianglesWithJoinFaces) {
                    if(j > 3) {
                        j = 0;
                        values += "\n";
                    }
                    values += ";" + i;
                }
            }
            return mesh;
        }


        public void Update()
        {
            if(!assets_initialised && showHide3D) {
                init_assets();
            }
            if(tryMesh) {
                foreach(Actor actor in MapBox.instance.units) {
                    if(actor != null && !this.meshdActors.Contains(actor)) {
                        SpriteRenderer spriteRenderer = Reflection.GetField(actor.GetType(), actor, "spriteRenderer") as SpriteRenderer;
                        Shader oldShader = spriteRenderer.material.shader;
                        Sprite sprite = spriteRenderer.sprite;
                        int sortingLayerID = spriteRenderer.sortingLayerID;
                        string sortingLayerName = spriteRenderer.sortingLayerName;
                        Material material2 = spriteRenderer.material;

                        GameObject gameObject1 = new GameObject("balls");
                        gameObject1.transform.position = spriteRenderer.transform.position;

                        Texture2D texture2D = Voxelizer.VoxelMenu.duplicateTexture(Voxelizer.VoxelMenu.ReadTexture(spriteRenderer.sprite.texture));
                        Mesh mesh = Voxelizer.VoxelUtil.VoxelizeTexture2D(texture2D, false, 1f);
                        Texture2D texture2D2 = Voxelizer.VoxelUtil.GenerateTextureMap(ref mesh, texture2D);
                        gameObject1.AddComponent<MeshFilter>().sharedMesh = mesh;
                        //gameObject1.transform.parent = actor.transform.parent;
                        //gameObject1.transform.localPosition = actor.gameObject.transform.localPosition;
                        MeshRenderer meshRenderer1 = gameObject1.AddComponent<MeshRenderer>();
                        if(texture2D2 != null) {
                            Material material = new Material(oldShader);
                            material.SetTexture("_MainTex", texture2D2);
                            meshRenderer1.sharedMaterial = material;
                        }
                        this.meshdActors.Add(actor);
                    }

                    if(true == false) { //meshdActors.Contains(actor) == false

                        SpriteRenderer spriteRenderer = Reflection.GetField(actor.GetType(), actor, "spriteRenderer") as SpriteRenderer;
                        Sprite actorSprite = spriteRenderer.sprite;

                        int layer = spriteRenderer.sortingLayerID;
                        string layerName = spriteRenderer.sortingLayerName;

                        //spriteToMesh(actorSprite, 5);
                        Material actorMaterial = new Material(Shader.Find("Standard"));
                        actorMaterial.SetTexture("_MainTex", spriteRenderer.sprite.texture);

                        //Texture2D actorTexture = spriteRenderer.sprite.texture;
                        Texture2D actorTexture = Voxelizer.VoxelMenu.ReadTexture(spriteRenderer.sprite.texture);


                        Mesh actorAsMesh = Voxelizer.VoxelUtil.VoxelizeTexture2D(actorTexture, false, 1f);
                        Texture2D texture = Voxelizer.VoxelUtil.GenerateTextureMap(ref actorAsMesh, actorTexture);

                        //meshes.Add(new meshT { Actor = actor, mat = actorMaterial, shader = actorShader, mesh = actorAsMesh });

                        GameObject.DestroyImmediate(actor.GetComponent(typeof(SpriteRenderer)) as SpriteRenderer);

                        MeshFilter filter = actor.gameObject.GetComponent(typeof(MeshFilter)) as MeshFilter;
                        if(filter == null) {
                            filter = actor.gameObject.AddComponent(typeof(MeshFilter)) as MeshFilter;
                        }
                        MeshRenderer render = actor.gameObject.GetComponent(typeof(MeshRenderer)) as MeshRenderer;
                        if(render == null) {
                            render = actor.gameObject.AddComponent(typeof(MeshRenderer)) as MeshRenderer;
                        }

                        actorAsMesh.RecalculateBounds();
                        actorAsMesh.RecalculateNormals();
                        filter.mesh = actorAsMesh;
                        /*
                        filter.sharedMesh = actorAsMesh;
                        filter.sharedMesh.RecalculateBounds();
                        filter.sharedMesh.RecalculateNormals();
                        */
                        render.sortingLayerID = layer;
                        render.sortingLayerName = layerName;
                        render.material = actorMaterial;
                        render.sharedMaterial = actorMaterial;
                        render.material.mainTexture = actorTexture;
                        meshdActors.Add(actor);
                    }
                    if(meshdActors.Contains(actor) == true) {
                        MeshRenderer meshRender = actor.gameObject.GetComponent(typeof(MeshRenderer)) as MeshRenderer;
                        if(meshRender != null) {

                        }
                    }
                    //render.material = 
                    //newMaterial.
                    //Material newMaterial = new Material(actorMaterial);
                    foreach(meshT m in meshes) {
                        Graphics.DrawMesh(m.mesh, position: actor.currentTile.posV3, rotation: Quaternion.Euler(-90, 0, 0), m.mat, 5000);
                    }
                }

            }

        }
        public void Awake()
        {
            Debug.Log("WorldBox3D loaded");
            Harmony harmony = new Harmony(pluginGuid);
            MethodInfo original = AccessTools.Method(typeof(Actor), "updatePos");
            MethodInfo patch = AccessTools.Method(typeof(_3D_Main), "updateActorPos_Postfix");
            harmony.Patch(original, null, new HarmonyMethod(patch));

            harmony = new Harmony(pluginGuid);
            original = AccessTools.Method(typeof(Cloud), "update");
            patch = AccessTools.Method(typeof(_3D_Main), "update_CloudPostfix");
            harmony.Patch(original, null, new HarmonyMethod(patch));
            Debug.Log("Post patch: Cloud.update");

            harmony = new Harmony(pluginGuid);
            original = AccessTools.Method(typeof(SpriteAnimation), "update");
            patch = AccessTools.Method(typeof(_3D_Main), "updateSpriteAnimation_Prefix");
            harmony.Patch(original, new HarmonyMethod(patch));
            Debug.Log("updateSpriteAnimation_Prefix");

            harmony = new Harmony(pluginGuid);
            original = AccessTools.Method(typeof(Drop), "updatePosition");
            patch = AccessTools.Method(typeof(_3D_Main), "updateDropPos_Prefix");
            harmony.Patch(original, new HarmonyMethod(patch));
            Debug.Log("updateDropPos_Prefix");


            harmony = new Harmony(pluginGuid);
            original = AccessTools.Method(typeof(Actor), "punchTargetAnimation");
            patch = AccessTools.Method(typeof(_3D_Main), "punchTargetAnimation_Prefix");
            harmony.Patch(original, new HarmonyMethod(patch));
            Debug.Log("punchTargetAnimation");

            harmony = new Harmony(pluginGuid);
            original = AccessTools.Method(typeof(ActorBase), "updateFlipRotation");
            patch = AccessTools.Method(typeof(_3D_Main), "updateFlipRotation_Prefix");
            harmony.Patch(original, new HarmonyMethod(patch));
            Debug.Log("updateFlipRotation_Prefix");

            harmony = new Harmony(pluginGuid);
            original = AccessTools.Method(typeof(ActorBase), "updateRotationBack");
            patch = AccessTools.Method(typeof(_3D_Main), "updateRotationBack_Prefix");
            harmony.Patch(original, new HarmonyMethod(patch));
            Debug.Log("updateRotationBack_Prefix");

            harmony = new Harmony(pluginGuid);
            original = AccessTools.Method(typeof(TilemapExtended), "setTile");
            patch = AccessTools.Method(typeof(_3D_Main), "setTile_Prefix");
            harmony.Patch(original, new HarmonyMethod(patch));
            Debug.Log("setTile_Prefix");

            harmony = new Harmony(pluginGuid);
            original = AccessTools.Method(typeof(BaseEffect), "setAlpha"); // applies to clouds, explosions, fireworks
            patch = AccessTools.Method(typeof(_3D_Main), "setAlpha_Postfix");
            harmony.Patch(original, null, new HarmonyMethod(patch));
            Debug.Log("setAlpha_Postfix");

            harmony = new Harmony(pluginGuid);
            original = AccessTools.Method(typeof(BurnedTilesLayer), "setTileDirty");
            patch = AccessTools.Method(typeof(_3D_Main), "setTileDirty_Prefix");
            harmony.Patch(original, new HarmonyMethod(patch));
            Debug.Log("setTileDirty_Prefix");

            harmony = new Harmony(pluginGuid);
            original = AccessTools.Method(typeof(FireLayer), "setTileDirty");
            patch = AccessTools.Method(typeof(_3D_Main), "setTileDirty_Prefix");
            harmony.Patch(original, new HarmonyMethod(patch));
            Debug.Log("setTileDirty_Prefix");

            harmony = new Harmony(pluginGuid);
            original = AccessTools.Method(typeof(GroupSpriteObject), "setRotation");
            //patch = AccessTools.Method(typeof(_3D_Main), "setRotation_Postfix");
            //harmony.Patch(original, new HarmonyMethod(patch));
            //Debug.Log("setTileDirty_Prefix");



            Debug.Log("Post patch: Actor.updatePos");
            SettingSetup();
        }
        public static bool setTileDirty_Prefix(WorldTile pTile)
        {
            if(_3dEnabled && tile3Denabled) {
                return false;
            }
            return true;
        }
        public static WorldTile tileFromVector(Vector3 input)
        {
            WorldTile returnTile = MapBox.instance.GetTile((int)input.x, (int)input.y);
            if(returnTile != null) {
                return returnTile;
            }
            return null;
        }

        public int BuildingThickness(Building target)
        {
            int thickness = thickenCount;
            BuildingAsset stats = Reflection.GetField(target.GetType(), target, "stats") as BuildingAsset;
            if(buildingCustomThickness.ContainsKey(stats.id)) {
                thickness = buildingCustomThickness[stats.id];
            }
            return thickness;
        }

        public int BuildingAngle(Building target)
        {
            int angle = 90;
            BuildingAsset stats = Reflection.GetField(target.GetType(), target, "stats") as BuildingAsset;
            if(buildingCustomAngle.ContainsKey(stats.id)) {
                angle = buildingCustomAngle[stats.id];
            }
            if(angle > 360) {
                angle = 360;
            }
            return angle;
        }


        public float BuildingHeight(Building target)
        {
            float height = 0f;
            BuildingAsset stats = Reflection.GetField(target.GetType(), target, "stats") as BuildingAsset;

            if(buildingCustomHeight.ContainsKey(stats.id)) {
                height = buildingCustomHeight[stats.id];
            }
            if(tile3Denabled) {
                if(buildingCustomHeight.ContainsKey(stats.id)) {
                    height = buildingCustomHeight[stats.id] + (target.currentTile.Height / dividerAmount);

                }
                else {
                    height = target.currentTile.Height / dividerAmount;
                }
            }
            return -height;
        }

        public static void setAlpha_Postfix(float pVal, BaseEffect __instance) // applies to clouds, explosions, fireworks
        {
            if(_3dEnabled) {
                float height = 0f;
                WorldTile tile = tileFromVector(__instance.transform.localPosition);
                if(tile3Denabled && tile != null) {
                    height = (-tile.Height) / dividerAmount;
                }

                __instance.transform.localPosition = new Vector3(__instance.transform.localPosition.x, __instance.transform.localPosition.y, height);
                __instance.transform.rotation = Quaternion.Euler(-90, 0, 0); // Quaternion.Euler(-90, 0, 0);
            }
        }
        public static bool updateFlipRotation_Prefix(float pElapsed, ActorBase __instance)
        {
            if(_3dEnabled) {
                return false;
            }
            return true;
        }
        public static bool updateRotationBack_Prefix(float pElapsed, ActorBase __instance)
        {
            if(_3dEnabled) {
                return false;
            }
            return true;
        }
        /*
        public static bool updateRotation_Prefix(ActorBase __instance)
        {
            if (spriteReplacedActors.Contains(__instance))
            {
                return false;
            }
            return true;
        }
        */
        public static bool punchTargetAnimation_Prefix(Vector3 pDirection, WorldTile pTile, bool pFlip, bool pReverse, float pAngle, Actor __instance)
        {
            if(_3dEnabled) {
                return false;
            }
            return true;
        }

        public static bool updateSpriteAnimation_Prefix(SpriteAnimation __instance)
        {
            if(_3dEnabled) {
                __instance.transform.rotation = Quaternion.Euler(-90, 0, 0); // Quaternion.Euler(-90, 0, 0);
                return true;
            }
            return true;
        }
        public static bool updateDropPos_Prefix(Drop __instance)
        {
            if(_3dEnabled) {
                __instance.transform.localPosition = new Vector3(__instance.currentPosition.x, __instance.currentPosition.y, -__instance.zPosition.z);
                return false;
            }
            return true;
        }
        public static void updateActorPos_Postfix(Actor __instance)
        {
            float height = 0f;
            if(tile3Denabled) {
                height = (-__instance.currentTile.Height) / dividerAmount;
            }
            if(_3dEnabled) {
                __instance.transform.localPosition = new Vector3(__instance.transform.localPosition.x, __instance.transform.localPosition.y, height);
            }
        }
        public static LineRenderer singleLine;
        public static List<LineRenderer> tileTypeLines;
        public void window3D(int windowID)
        {
            if(GUILayout.Button("tryMesh: " + tryMesh.ToString())) {
                tryMesh = !tryMesh;
            }
            //cameraTransform.rotation = new Quaternion(cameraTransform.rotation.x, cameraTransform.rotation.y, 0f, cameraTransform.rotation.w);
            if(Input.GetKeyUp(KeyCode.R)) {
                if(activeLines != null && activeLines.Count > 1) {
                    foreach(LineRenderer line in activeLines) {
                        line.SetVertexCount(0);
                    }
                }
                if(tileTypeLines != null && tileTypeLines.Count > 1) {
                    foreach(LineRenderer line2 in tileTypeLines) {
                        line2.SetVertexCount(0);
                    }
                }
                singleLine.SetVertexCount(0);
                deleteAllExtraLayers(); // sprite thickening reset
            }
            if(GUILayout.Button("tile3d: " + tile3Denabled.ToString())) {
                tile3Denabled = !tile3Denabled;
            }
            if(GUILayout.Button("Reset camera")) {
                Camera.main.transform.rotation = Quaternion.Euler(cameraX, cameraY, cameraZ);
            }
            if(GUILayout.Button("Enable 3D: " + autoPlacement)) {
                autoPlacement = !autoPlacement;
                _3dEnabled = !_3dEnabled;
            }

            if(GUILayout.Button("Spawn 100 cloud hurricane (C)") || Input.GetKeyDown(KeyCode.C)) {
                SpawnCloudsInCircle(MapBox.instance.getMouseTilePos(), 100); // hurricane spin is bugged, so this is disabled
            }

            try {
                ObjectPositioningButtons();
            }
            catch(System.Exception e) {
                // prevent null error and group repaint error from showing in log.. sloppy but the errors dont mess anything up anyway
            }
            //ObjectRotationButtons();
            CameraControls();
            if(showLineButtons) {
                if(GUILayout.Button("Single line per tile type")) {
                    /*
                    int scaleFactor = 5;
                    if(tileTypeLines == null) {
                        tileTypeLines = new List<LineRenderer>();

                    }
                    int currentVertexCount = 0;
                   //fuck this for now
                    // List<TileTypeBase> tileTypes = AssetManager.tiles.list.AddRange(AssetManager.topTiles.list);

                    foreach(TileType tileType in) {
                        List<WorldTile> tilesOfType = new List<WorldTile>();
                        foreach(WorldTile tile in MapBox.instance.tilesList) {
                            if(tile.Type == tileType) {
                                tilesOfType.Add(tile);
                            }
                        }
                        LineRenderer tileTypesNewLine = new GameObject("Line").AddComponent<LineRenderer>();
                        LineRenderer lineRenderer = tileTypesNewLine;
                        lineRenderer.SetWidth(1f, 1f);
                        lineRenderer.SetColors(tileType.color, tileType.color);
                        Material whiteDiffuseMat = new Material(Shader.Find("UI/Default"));
                        lineRenderer.material = whiteDiffuseMat;
                        lineRenderer.material.color = tileType.color;
                        lineRenderer.SetVertexCount(tilesOfType.Count);
                        for(int i = 2; i < tilesOfType.Count; i++) {
                            WorldTile tile = tilesOfType[i];
                            lineRenderer.SetPosition(i - 2, new Vector3(tile.posV3.x, tile.posV3.y, (-(float)tile.Height / scaleFactor) - 150f));
                            lineRenderer.SetPosition(i - 1, new Vector3(tile.posV3.x, tile.posV3.y, (-(float)tile.Height / scaleFactor) - 150f) + new Vector3(0, 0, -10));
                            currentVertexCount++;
                        }
                        tileTypeLines.Add(lineRenderer);
                    }
                    */
                }

                if(GUILayout.Button("Single line for all tiles")) {
                    int scaleFactor = 5;
                    Color color1 = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
                    Color color2 = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
                    if(singleLine == null) {
                        singleLine = new GameObject("Line").AddComponent<LineRenderer>();
                    }
                    LineRenderer lineRenderer = singleLine;
                    lineRenderer.SetWidth(1f, 1f);
                    lineRenderer.SetVertexCount(MapBox.instance.tilesList.Count);
                    lineRenderer.SetColors(color1, color2);
                    Material whiteDiffuseMat = new Material(Shader.Find("UI/Default"));
                    lineRenderer.material = whiteDiffuseMat;
                    lineRenderer.material.color = color1;
                    for(int i = 2; i < MapBox.instance.tilesList.Count; i++) {
                        WorldTile tile = MapBox.instance.tilesList[i];
                        lineRenderer.SetPosition(i - 2, new Vector3(tile.posV3.x, tile.posV3.y, (-(float)tile.Height / scaleFactor) - 150f));
                        lineRenderer.SetPosition(i - 1, new Vector3(tile.posV3.x, tile.posV3.y, (-(float)tile.Height / scaleFactor) - 150f) + new Vector3(0, 0, -10));
                    }
                }
                if(GUILayout.Button("Single line for all tiles except liquid")) {
                    int scaleFactor = 5;
                    if(singleLine == null) {
                        singleLine = new GameObject("Line").AddComponent<LineRenderer>();
                    }
                    LineRenderer lineRenderer = singleLine;
                    lineRenderer.SetWidth(1f, 1f);
                    lineRenderer.SetVertexCount(MapBox.instance.tilesList.Count);
                    lineRenderer.SetColors(Color.green, Color.red);
                    Material whiteDiffuseMat = new Material(Shader.Find("UI/Default"));
                    lineRenderer.material = whiteDiffuseMat;
                    lineRenderer.material.color = Color.green;
                    for(int i = 2; i < MapBox.instance.tilesList.Count; i++) {
                        if(!MapBox.instance.tilesList[i].Type.liquid) {
                            WorldTile tile = MapBox.instance.tilesList[i];
                            lineRenderer.SetPosition(i - 2, new Vector3(tile.posV3.x, tile.posV3.y, (-(float)tile.Height / scaleFactor) - 150f));
                            lineRenderer.SetPosition(i - 1, new Vector3(tile.posV3.x, tile.posV3.y, (-(float)tile.Height / scaleFactor) - 150f) + new Vector3(0, 0, -10));
                            activeLines.Add(lineRenderer);
                        }
                    }
                }
                if(GUILayout.Button("Line for every tile on the map")) {
                    int scaleFactor = 5;
                    foreach(WorldTile tile in MapBox.instance.tilesList) {
                        // if (!tile.Type.water)
                        // {
                        Color tileColor = tile.getColor();
                        LineRenderer lineRenderer = new GameObject("Line").AddComponent<LineRenderer>();
                        lineRenderer.SetWidth(1f, 1f);
                        lineRenderer.startColor = tileColor;
                        lineRenderer.endColor = tileColor;
                        lineRenderer.SetColors(tileColor, tileColor);
                        lineRenderer.SetVertexCount(2);
                        lineRenderer.SetPosition(0, new Vector3(tile.posV3.x, tile.posV3.y, (-(float)tile.Height / scaleFactor) - 150f));
                        lineRenderer.SetPosition(1, new Vector3(tile.posV3.x, tile.posV3.y, (-(float)tile.Height / scaleFactor) - 150f) + new Vector3(0, 0, -10));
                        Material whiteDiffuseMat = new Material(Shader.Find("UI/Default"));
                        lineRenderer.material = whiteDiffuseMat;
                        lineRenderer.material.color = tileColor;
                        // lineRenderer.sortingOrder = 50;
                        activeLines.Add(lineRenderer);
                        // }
                    }
                }
                if(GUILayout.Button("Line for every tile on the map except water")) {
                    int scaleFactor = 5;
                    foreach(WorldTile tile in MapBox.instance.tilesList) {
                        if(!tile.Type.liquid) {
                            Color tileColor = tile.getColor();
                            LineRenderer lineRenderer = new GameObject("Line").AddComponent<LineRenderer>();
                            lineRenderer.SetWidth(1f, 1f);
                            lineRenderer.startColor = tileColor;
                            lineRenderer.endColor = tileColor;
                            lineRenderer.SetColors(tileColor, tileColor);
                            lineRenderer.SetVertexCount(2);
                            lineRenderer.SetPosition(0, new Vector3(tile.posV3.x, tile.posV3.y, (-(float)tile.Height / scaleFactor) - 150f));
                            lineRenderer.SetPosition(1, new Vector3(tile.posV3.x, tile.posV3.y, (-(float)tile.Height / scaleFactor) - 150f) + new Vector3(0, 0, -10));
                            Material whiteDiffuseMat = new Material(Shader.Find("UI/Default"));
                            lineRenderer.material = whiteDiffuseMat;
                            lineRenderer.material.color = tileColor;
                            // lineRenderer.sortingOrder = 50;
                            activeLines.Add(lineRenderer);
                        }
                    }
                }

                if(GUILayout.Button("Test light")) {

                    // light 1
                    GameObject newLightObject = new GameObject();
                    newLightObject.AddComponent<Light>();
                    Light objectsLight = newLightObject.GetComponent<Light>();
                    objectsLight.type = LightType.Directional;
                    newLightObject.transform.DOBlendableRotateBy(new Vector3(-40, 0, 0), 5f, RotateMode.Fast);
                    // light 2
                    /*
                    GameObject newLightObject2 = new GameObject();
                    newLightObject2.AddComponent<Light>();
                    Light objectsLight2 = newLightObject2.GetComponent<Light>();
                    objectsLight2.type = LightType.Directional;

                       objectsLight2.intensity = 1;
                    objectsLight2.range = 3f;
                    objectsLight2.spotAngle = 60;
                                        newLightObject2.transform.position = MapBox.instance.GetTile(MapBox.width / 2, MapBox.height / 2).posV3;

                    */

                    Shader newshader = loadedAssetBundle.LoadAsset<Shader>("CustomDiffuse");

                    List<Material> shaders = Resources.FindObjectsOfTypeAll<Material>().ToList();
                    shaders.Where(x => x.name.Contains("MatWorld") == true).ToList().First().shader = newshader;


                    //?
                    objectsLight.intensity = 1;
                    objectsLight.range = 3f;
                    objectsLight.spotAngle = 60;
                    //?


                    newLightObject.transform.position = MapBox.instance.GetTile(MapBox.width / 2, MapBox.height / 2).posV3;

                }
                if(GUILayout.Button("PL test 2")) {
                    foreach(Building building in MapBox.instance.buildings) {
                        lightMan.buildingLight(building);
                    }
                }
                if(GUILayout.Button("Lights Intensity++")) {
                    foreach(Light light in lightMan.allLights) {
                        light.intensity = light.intensity + 0.1f;
                    }
                }
                if(GUILayout.Button("Lights Intensity--")) {
                    foreach(Light light in lightMan.allLights) {
                        light.intensity = light.intensity - 0.1f;
                    }
                }
                if(GUILayout.Button("Lights Range++")) {
                    foreach(Light light in lightMan.allLights) {
                        light.range = light.range + 0.1f;
                    }
                }
                if(GUILayout.Button("Lights Range--")) {
                    foreach(Light light in lightMan.allLights) {
                        light.range = light.range - 0.1f;
                    }
                }
                if(GUILayout.Button("White")) {
                    foreach(Light light in lightMan.allLights) {
                        light.color = Color.white;
                    }
                }
                if(GUILayout.Button("red")) {
                    foreach(Light light in lightMan.allLights) {
                        light.color = Color.red;
                    }
                }
                if(GUILayout.Button("Random")) {
                    foreach(Light light in lightMan.allLights) {
                        light.color = new Color(
      UnityEngine.Random.Range(0f, 1f),
      UnityEngine.Random.Range(0f, 1f),
      UnityEngine.Random.Range(0f, 1f)
  );
                    }
                }
                if(GUILayout.Button("Spot")) {
                    foreach(Light light in lightMan.allLights) {
                        light.type = LightType.Spot;
                    }
                }
                if(GUILayout.Button("Point")) {
                    foreach(Light light in lightMan.allLights) {
                        light.type = LightType.Point;
                    }
                }

                if(GUILayout.Button("Up")) {
                    foreach(Building building in MapBox.instance.buildings) {
                        if(lightMan.lightDict.ContainsKey(building.data.objectID)) {
                            GameObject buildingLight = lightMan.lightDict[building.data.objectID];
                            buildingLight.transform.localPosition = buildingLight.transform.localPosition + new Vector3(0f, 0f, -1f);
                        }
                    }
                }
                if(GUILayout.Button("Down")) {
                    foreach(Building building in MapBox.instance.buildings) {
                        if(lightMan.lightDict.ContainsKey(building.data.objectID)) {
                            GameObject buildingLight = lightMan.lightDict[building.data.objectID];
                            buildingLight.transform.localPosition = buildingLight.transform.localPosition + new Vector3(0f, 0f, 1f);
                        }
                    }
                }
                if(GUILayout.Button("For")) {
                    foreach(Building building in MapBox.instance.buildings) {
                        if(lightMan.lightDict.ContainsKey(building.data.objectID)) {
                            GameObject buildingLight = lightMan.lightDict[building.data.objectID];
                            buildingLight.transform.localPosition = buildingLight.transform.localPosition + new Vector3(0f, 1f, 0f);
                        }
                    }
                }
                if(GUILayout.Button("Back")) {
                    foreach(Building building in MapBox.instance.buildings) {
                        if(lightMan.lightDict.ContainsKey(building.data.objectID)) {
                            GameObject buildingLight = lightMan.lightDict[building.data.objectID];
                            buildingLight.transform.localPosition = buildingLight.transform.localPosition + new Vector3(0f, -1f, 0);
                        }
                    }
                }
                if(GUILayout.Button("Left")) {
                    foreach(Building building in MapBox.instance.buildings) {
                        if(lightMan.lightDict.ContainsKey(building.data.objectID)) {
                            GameObject buildingLight = lightMan.lightDict[building.data.objectID];
                            buildingLight.transform.localPosition = buildingLight.transform.localPosition + new Vector3(-1f, 0f, 0);
                        }
                    }
                }
                if(GUILayout.Button("Right")) {
                    foreach(Building building in MapBox.instance.buildings) {
                        if(lightMan.lightDict.ContainsKey(building.data.objectID)) {
                            GameObject buildingLight = lightMan.lightDict[building.data.objectID];
                            buildingLight.transform.localPosition = buildingLight.transform.localPosition + new Vector3(1f, 0f, 0);
                        }
                    }
                }

            }

            GUI.DragWindow();
        }

        public class LightManager {
            public Dictionary<string, GameObject> lightDict = new Dictionary<string, GameObject>();
            public List<Light> allLights = new List<Light>();
            public Light buildingLight(Building pBuilding)
			{
                if(lightDict.ContainsKey(pBuilding.data.objectID)) {
                    return lightDict[pBuilding.data.objectID].GetComponent<Light>();
                }
				else {
                    // light 1
                    GameObject newLightObject = new GameObject();
                    newLightObject.AddComponent<Light>();
                    Light objectsLight = newLightObject.GetComponent<Light>();
                    objectsLight.type = LightType.Point;
                    allLights.Add(objectsLight);
                    newLightObject.transform.position = pBuilding.currentTile.posV3 + new Vector3(0f, 0, -1);
                    lightDict.Add(pBuilding.data.objectID, newLightObject);
                    return objectsLight;
                }

            }
        }

        public GameObject Light(Building pParent)
		{
            // light 2
            GameObject newLightObject2 = pParent.gameObject;
            Light objectsLight2;
            if(pParent.TryGetComponent<Light>(out Light potentialLight)) {
                objectsLight2 = potentialLight;
            }
			else {
                newLightObject2.AddComponent<Light>();
                objectsLight2 = newLightObject2.GetComponent<Light>();
                newLightObject2.transform.SetParent(pParent.transform);
            }
            return newLightObject2;
        }



        static AssetBundle loadedAssetBundle;

        void init_assets()
        {
            LoadAssetBundle();
        }


        public void LoadAssetBundle()
        {
            string bundlename = "unleashed";
            loadedAssetBundle = AssetBundle.LoadFromFile(Path.Combine(Application.streamingAssetsPath, "AssetBundle", bundlename));
            if(loadedAssetBundle == null) {
                Debug.Log("Failed to load AssetBundle");
                assets_initialised = false;
                return;
            }

            assets_initialised = true;
        }

        static bool assets_initialised;

        public bool showLineButtons = true;

        public bool showHide3D;
        public Rect window3DRect;
        public void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 120, 50, 120, 30));
            if(GUILayout.Button("WorldBox3D")) // "WorldBox3D"
            {
                showHide3D = !showHide3D;
            }
            GUILayout.EndArea();
            if(showHide3D) {
                GUI.contentColor = UnityEngine.Color.white;
                window3DRect = GUILayout.Window(11015, window3DRect, new GUI.WindowFunction(window3D), "WorldBox3D", new GUILayoutOption[] { GUILayout.MaxWidth(300f), GUILayout.MinWidth(200f) });
            }
        }
        // unused thickening test
        /*
        public static SpriteRenderer createNewBodyPart_Postfix(SpriteRenderer __result, string pName)
        {
            for (int i = 0; i <= 100; i++)
            {
                SpriteRenderer newSprite = Instantiate(__result);

                newSprite.transform.localPosition = new Vector3(newSprite.transform.localPosition.x, newSprite.transform.localPosition.y + (float)i / 10f, newSprite.transform.localPosition.z);

            }
            return __result;
        }
        */
        public List<SpriteRenderer> extraLayers = new List<SpriteRenderer>();

        public void layerActorSprite(Actor targetActor)
        {
            SpriteRenderer spriteRenderer = Reflection.GetField(targetActor.GetType(), targetActor, "spriteRenderer") as SpriteRenderer;
            SpriteRenderer newSprite = Instantiate(spriteRenderer) as SpriteRenderer;
            SpriteRenderer newSprite2 = Instantiate(spriteRenderer) as SpriteRenderer;
            newSprite2.transform.rotation = Quaternion.Euler(0, 90, -90);
            extraLayers.Add(newSprite);
            extraLayers.Add(newSprite2);

        }
        public void layerBuildingSprite(Building targetBuilding)
        {
            SpriteRenderer spriteRenderer = Reflection.GetField(targetBuilding.GetType(), targetBuilding, "spriteRenderer") as SpriteRenderer;
            SpriteRenderer newSprite = Instantiate(spriteRenderer) as SpriteRenderer;
            SpriteRenderer newSprite2 = Instantiate(spriteRenderer) as SpriteRenderer;
            newSprite2.transform.rotation = Quaternion.Euler(0, 90, -90);
            extraLayers.Add(newSprite);
            extraLayers.Add(newSprite2);

        }
        public void thickenActorSprite(Actor targetActor)
        {
            Actor newActor = Instantiate(targetActor) as Actor; // why dont i use the original?? check later
            newActor.transform.parent = targetActor.transform;
            SpriteRenderer spriteRenderer = Reflection.GetField(newActor.GetType(), newActor, "spriteRenderer") as SpriteRenderer;
            /*
            // new stuff
            Sprite actorSprite = spriteRenderer.sprite;
            Mesh actorAsMesh = SpriteToMesh(actorSprite);
            MeshFilter filter = newActor.gameObject.AddComponent(typeof(MeshFilter)) as MeshFilter;
            MeshRenderer render = newActor.gameObject.AddComponent(typeof(MeshRenderer)) as MeshRenderer;
            filter.mesh = actorAsMesh;
            //render.material = 
            Material newMaterial = new Material(spriteRenderer.material);
            //newMaterial.
            Graphics.DrawMesh(actorAsMesh, position: newActor.currentTile.posV3, rotation: Quaternion.Euler(-90, 0, 0), newMaterial, 0);
            // end new stuff
            */
            int distanceScaling = 25;
            for(int i = 0; i <= 5; i++) {
                SpriteRenderer newSprite = Instantiate(spriteRenderer) as SpriteRenderer;
                newSprite.transform.localPosition = spriteRenderer.transform.localPosition + new Vector3(0f, (0f + (float)i) / distanceScaling, 0f);
                extraLayers.Add(newSprite);
            }
            for(int i = 0; i <= 5; i++) {
                SpriteRenderer newSprite = Instantiate(spriteRenderer) as SpriteRenderer;
                newSprite.transform.localPosition = spriteRenderer.transform.localPosition + new Vector3(0f, -(0f + (float)i) / distanceScaling, 0f);
                extraLayers.Add(newSprite);
            }
        }
        public void thickenActorSpriteRotated(Actor targetActor)
        {
            Actor newActor = Instantiate(targetActor) as Actor;
            newActor.transform.parent = targetActor.transform;
            newActor.transform.rotation = Quaternion.Euler(0, 90, -90);
            SpriteRenderer spriteRenderer = Reflection.GetField(newActor.GetType(), newActor, "spriteRenderer") as SpriteRenderer;
            int distanceScaling = 25;
            for(int i = 0; i <= 15; i++) {
                SpriteRenderer newSprite = Instantiate(spriteRenderer) as SpriteRenderer;
                newSprite.transform.localPosition = spriteRenderer.transform.localPosition + new Vector3((0f + (float)i) / distanceScaling, 0f, 0f);
                extraLayers.Add(newSprite);
            }
            for(int i = 0; i <= 15; i++) {
                SpriteRenderer newSprite = Instantiate(spriteRenderer) as SpriteRenderer;
                newSprite.transform.localPosition = spriteRenderer.transform.localPosition + new Vector3(-(0f + (float)i) / distanceScaling, 0f, 0f);
                extraLayers.Add(newSprite);
            }
        }
        public void deleteAllExtraLayers()
        {
            for(int i = 0; i < extraLayers.Count; i++) {
                Destroy(extraLayers[i]);
            }
        }
        public bool rotateBuildingBecauseAssetLoader(Building target) // assetload rotate buildings
        {
            bool returnBool = false;
            BuildingAsset stats = Reflection.GetField(target.GetType(), target, "stats") as BuildingAsset;
            if(buildingCustomAngle.ContainsKey(stats.id)) // weird combo to enable rotation, need something better
            {
                returnBool = true;
            }
            return returnBool;
        }

        int thickenCount = 3;
        int distanceScaling = 25;
        // upgradelevel assigned through assetloader, custom thickness for 3d (stats.upgradeLevel + 1) * 4; neat value
        public void thickenBuilding(Building targetBuilding)
        {
            int layerCount = 0;
            if(rotateBuildingBecauseAssetLoader(targetBuilding)) {
                thickenBuildingRotated(targetBuilding);
            }
            else {
                BuildingAsset stats = Reflection.GetField(targetBuilding.GetType(), targetBuilding, "stats") as BuildingAsset;
                layerCount = BuildingThickness(targetBuilding);
                if(layerCount <= 5) {
                    layerCount = 5;
                }// whew this could be simpler
                if(targetBuilding.city != null) {
                    layerCount *= regularThickeningCityMultiplier.Value;
                }
                SpriteRenderer spriteRenderer = Reflection.GetField(targetBuilding.GetType(), targetBuilding, "spriteRenderer") as SpriteRenderer;
                spriteRenderer.transform.rotation = Quaternion.Euler(-90, 0, 0);
                for(int i = 0; i <= layerCount; i++) {
                    SpriteRenderer newSprite = Instantiate(spriteRenderer) as SpriteRenderer;
                    newSprite.transform.localPosition = targetBuilding.transform.localPosition + new Vector3(0f, (0f + (float)i) / distanceScaling, 0f);
                    extraLayers.Add(newSprite);
                }
                for(int i = 0; i <= layerCount; i++) {
                    SpriteRenderer newSprite = Instantiate(spriteRenderer) as SpriteRenderer;
                    newSprite.transform.localPosition = targetBuilding.transform.localPosition + new Vector3(0f, -(0f + (float)i) / distanceScaling, 0f);
                    extraLayers.Add(newSprite);
                }
            }
        }
        public void thickenBuildingRotated(Building targetBuilding)
        {
            int layerCount = 0;
            BuildingAsset stats = Reflection.GetField(targetBuilding.GetType(), targetBuilding, "stats") as BuildingAsset;
            layerCount = BuildingThickness(targetBuilding);
            if(layerCount <= 5) {
                layerCount = 5;
            }// whew this could be simpler
            if(targetBuilding.city != null) {
                layerCount *= regularThickeningCityMultiplier.Value;
            }
            SpriteRenderer spriteRenderer = Reflection.GetField(targetBuilding.GetType(), targetBuilding, "spriteRenderer") as SpriteRenderer;
            spriteRenderer.transform.rotation = Quaternion.Euler(0, 90, -90);
            for(int i = 0; i <= layerCount; i++) {
                SpriteRenderer newSprite = Instantiate(spriteRenderer) as SpriteRenderer;
                newSprite.transform.localPosition = spriteRenderer.transform.localPosition + new Vector3((0f + (float)i) / distanceScaling, 0f, 0f);
                extraLayers.Add(newSprite);
            }
            for(int i = 0; i <= layerCount; i++) {
                SpriteRenderer newSprite = Instantiate(spriteRenderer) as SpriteRenderer;
                newSprite.transform.localPosition = spriteRenderer.transform.localPosition + new Vector3(-(0f + (float)i) / distanceScaling, 0f, 0f);
                extraLayers.Add(newSprite);
            }
        }
        public void ObjectPositioningButtons()
        {
            PositionAndRotateUnits();
            PositionAndRotateBuildings();
            if(GUILayout.Button("Thickened Sprite - Buildings - Normal")) {
                foreach(Building building in MapBox.instance.buildings) {
                    thickenBuilding(building);
                }
            }
            if(GUILayout.Button("1 Rotated Sprite - Actors")) {
                foreach(Actor actor in MapBox.instance.units) {
                    layerActorSprite(actor);
                }
            }
            if(GUILayout.Button("1 Rotated Sprite - Buildings")) {
                foreach(Building building in MapBox.instance.buildings) {
                    layerBuildingSprite(building);
                }
            }
            /*
             *             if (GUILayout.Button("Thickened Sprite - Buildings - Rotated"))
            {
                foreach (Building building in MapBox.instance.buildings)
                {
                    thickenBuildingRotated(building, regularThickeningScale.Value);
                }
            }

            if (GUILayout.Button("Thickened Sprite - Actors - Normal"))
            {
                foreach (Actor actor in MapBox.instance.units)
                {
                    thickenActorSprite(actor);
                }
            }
            if (GUILayout.Button("Thickened Sprite - Actors - Rotated"))
            {
                foreach (Actor actor in MapBox.instance.units)
                {
                    thickenActorSpriteRotated(actor);
                }
            }
            */
            if(GUILayout.Button("Reset extra sprites")) {
                deleteAllExtraLayers();
            }
            if(showLineButtons) {
                if(GUILayout.Button("Place everything on linedRender")) {
                    foreach(Building building in MapBox.instance.buildings) {
                        WorldTile currentTile = Reflection.GetField(building.GetType(), building, "currentTile") as WorldTile;
                        MapObjectShadow shadow = Reflection.GetField(building.GetType(), building, "shadow") as MapObjectShadow;
                        if(currentTile != null) {
                            building.transform.position = new Vector3(building.transform.position.x, building.transform.position.y, (-(float)currentTile.Height / 5) - 150f) + new Vector3(0, 0, -10);
                            building.transform.rotation = Quaternion.Euler(-90, 0, 0);
                            shadow.transform.position = new Vector3(shadow.transform.position.x, shadow.transform.position.y, (-(float)currentTile.Height / 5) - 150f) + new Vector3(0, 0, -10);
                            shadow.transform.position = new Vector3(shadow.transform.position.x, shadow.transform.position.y, 0f);
                        }
                    }
                }
            }
        }

        private void PositionAndRotateBuildings()
        {
            if(autoPlacement && Toolbox.randomChance(manipulationRate)) {
                foreach(Building building in MapBox.instance.buildings) {
                    building.transform.position = new Vector3(building.transform.position.x, building.transform.position.y, BuildingHeight(building));
                    if(rotateBuildingBecauseAssetLoader(building)) {
                        building.transform.rotation = Quaternion.Euler(0, 90, -90);
                    }
                    else {
                        building.transform.rotation = Quaternion.Euler(-90, 0, 0);
                    }
                }
            }
        }

        private void PositionAndRotateUnits()
        {
            if(autoPlacement && Toolbox.randomChance(manipulationRate)) {
                foreach(Actor actor in MapBox.instance.units) {
                    float height = 0f;

                    if(actor.currentTile != null) {
                        if(tile3Denabled) {
                            height = (-actor.currentTile.Height) / dividerAmount;
                        }
                        actor.transform.position = new Vector3(actor.transform.position.x, actor.transform.position.y, height);
                        SpriteRenderer spriteRenderer = Reflection.GetField(actor.GetType(), actor, "spriteRenderer") as SpriteRenderer;
                        spriteRenderer.transform.rotation = Quaternion.Euler(-90, 0, 0);
						//actor.transform.rotation = Quaternion.Euler(-90, 0, 0);

                        //Vector3 curAngle = (Vector3)Reflection.GetField(actor.GetType(), actor, "curAngle");
                        //curAngle.(actor.transform.rotation);
                        //Sprite s_item_sprite = Reflection.GetField(actor.GetType(), actor, "s_item_sprite") as Sprite;
                        //s_item_sprite..rotation = Quaternion.Euler(-90, 0, 0);


                    }
                }
            }
        }
        /*
        public static void setRotation_Postfix(Vector3 pVec, GroupSpriteObject __instance)
        {
            if(_3dEnabled)
                __instance.transform.rotation = Quaternion.Euler(-90, 0, 0);

            //__instance.transform.position = new Vector3(__instance.transform.position.x, __instance.transform.position.y, 0f);
            Transform m_transform = (Transform)Reflection.GetField(__instance.GetType(), __instance, "m_transform");
            if(_3dEnabled)
                m_transform.rotation = Quaternion.Euler(-90, 0, 0);
            //m_transform.position = new Vector3(m_transform.transform.position.x, m_transform.transform.position.y, 0f);
        }
        */
        public void ObjectRotationButtons()
        {
            if(GUILayout.Button("Actor flip")) {
                foreach(Actor actor in MapBox.instance.units) {
                    SpriteAnimation spriteAnimation = Reflection.GetField(actor.GetType(), actor, "spriteAnimation") as SpriteAnimation;
                    spriteAnimation.transform.Rotate(-90, 0, 0); // was southup
                    actor.transform.position = new Vector3(actor.transform.position.x, actor.transform.position.y, 0f);
                }
            }
            if(GUILayout.Button("Building flip")) {
                foreach(Building building in MapBox.instance.buildings) {
                    SpriteRenderer spriteRenderer = Reflection.GetField(building.GetType(), building, "spriteRenderer") as SpriteRenderer;
                    spriteRenderer.transform.Rotate(-90, 0, 0);
                }
            }
            if(GUILayout.Button("RotateXBuildings")) {
                foreach(Building building in MapBox.instance.buildings) {

                    building.transform.position = new Vector3(building.transform.position.x, building.transform.position.y, 0f);
                    SpriteRenderer spriteRenderer = Reflection.GetField(building.GetType(), building, "spriteRenderer") as SpriteRenderer;
                    spriteRenderer.transform.Rotate(0, 90, 0);
                }
            }
            if(GUILayout.Button("RotateBuildingsXRandomized")) {
                foreach(Building building in MapBox.instance.buildings) {
                    if(Toolbox.randomBool()) {

                        building.transform.position = new Vector3(building.transform.position.x, building.transform.position.y, 0f);
                        SpriteRenderer spriteRenderer = Reflection.GetField(building.GetType(), building, "spriteRenderer") as SpriteRenderer;
                        spriteRenderer.transform.Rotate(0, 15, 0);
                    }

                }
            }
            if(GUILayout.Button("RotateActorsX")) {
                foreach(Actor building in MapBox.instance.units) {

                    building.transform.position = new Vector3(building.transform.position.x, building.transform.position.y, 0f);
                    SpriteRenderer spriteRenderer = Reflection.GetField(building.GetType(), building, "spriteRenderer") as SpriteRenderer;
                    spriteRenderer.transform.Rotate(0, 90, 0);
                }

            }
            if(GUILayout.Button("RotateActorsXRandomized")) {
                foreach(Actor actor in MapBox.instance.units) {
                    if(Toolbox.randomBool()) {
                        actor.transform.position = new Vector3(actor.transform.position.x, actor.transform.position.y, 0f);
                        SpriteRenderer spriteRenderer = Reflection.GetField(actor.GetType(), actor, "spriteRenderer") as SpriteRenderer;
                        spriteRenderer.transform.Rotate(0, 15, 0);
                    }

                }
            }
        }
        public void CameraControls()
        {
            Camera.main.nearClipPlane = -500f; // makes world stop clipping when camera rotates
            //Camera.main.useOcclusionCulling = false;
            if(_3dEnabled) {
                if(Input.GetKey(KeyCode.LeftAlt)) {
                    float x = rotationRate * Input.GetAxis("Mouse Y");
                    float y = rotationRate * -Input.GetAxis("Mouse X");
                    Camera.main.transform.Rotate(x, y, 0);
                }
                if(Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftAlt)) {
                    float x = rotationRate * Input.GetAxis("Mouse X");
                    float y = rotationRate * -Input.GetAxis("Mouse Y");
                    // MapBox.instance.GetTile(MapBox.width / 2, MapBox.height / 2); // CENTER TILE
                    Camera.main.transform.RotateAround(Input.mousePosition, -Vector3.up, rotationRate * Input.GetAxis("Mouse Y"));
                    Camera.main.transform.RotateAround(Vector3.zero, transform.right, rotationRate * Input.GetAxis("Mouse X"));
                }
            }

        }
    }

    
}

