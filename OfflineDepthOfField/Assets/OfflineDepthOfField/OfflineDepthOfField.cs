using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

public class OfflineDepthOfField : MonoBehaviour
{
    public enum SamplingType
    {
        Random,
        Hammersley
    }

    //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES ||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES ||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||| PUBLIC VARIABLES ||||||||||||||||||||||||||||||||||||||||||

    [Header("Rendering")]
    public SamplingType samplingType = SamplingType.Hammersley;
    public int samples = 2048;
    public Vector2Int resolution = new Vector2Int(1920, 1080);
    public ComputeShader averageBuffers;
    public bool retainPostProcessing;

    [Header("Parameters")]
    public float focalDistance = 1.0f;
    public Transform customFocalPoint;
    [Range(0.1f, 32.0f)] public float aperture = 2.8f;

    [Header("Gizmos")]
    public float gizmoSize;
    public bool showFocalPlane = true;

    //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||| PRIVATE VARIABLES ||||||||||||||||||||||||||||||||||||||||||

    private static int THREAD_GROUP_SIZE_X = 8;
    private static int THREAD_GROUP_SIZE_Y = 8;

    //|||||||||||||||||||||||||||||||||||||||||| MAIN CAPTURE METHOD ||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||| MAIN CAPTURE METHOD ||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||| MAIN CAPTURE METHOD ||||||||||||||||||||||||||||||||||||||||||

    [ContextMenu("Capture")]
    public void Capture()
    {
        UpdateProgressBar("Capturing Image", 0.5f);

        //|||||||||||||||||||||||||||||||||||||||||| CAPTURE SETUP ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| CAPTURE SETUP ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| CAPTURE SETUP ||||||||||||||||||||||||||||||||||||||||||

        if (averageBuffers == null)
        {
            Debug.LogError("Compute Shader Missing");
            CloseProgressBar();
            return;
        }

        Camera cameraComponent = GetComponent<Camera>();
        PostProcessLayer postProcessLayer = cameraComponent.GetComponent<PostProcessLayer>();
        Vector3 originalCameraPosition = transform.position;
        Quaternion originalCameraRotation = transform.rotation;

        RenderTexture computeShaderRenderTexture = new RenderTexture(resolution.x, resolution.y, 32, RenderTextureFormat.ARGBFloat);
        computeShaderRenderTexture.dimension = TextureDimension.Tex2D;
        computeShaderRenderTexture.enableRandomWrite = true;
        computeShaderRenderTexture.filterMode = FilterMode.Bilinear;
        computeShaderRenderTexture.Create();

        RenderTexture cameraRenderTexture = new RenderTexture(resolution.x, resolution.y, 32, RenderTextureFormat.ARGBFloat);
        cameraRenderTexture.dimension = TextureDimension.Tex2D;
        cameraRenderTexture.enableRandomWrite = true;
        cameraRenderTexture.filterMode = FilterMode.Bilinear;
        cameraRenderTexture.Create();

        cameraComponent.forceIntoRenderTexture = true;
        cameraComponent.targetTexture = cameraRenderTexture;

        int ComputeShader_AverageBuffers = averageBuffers.FindKernel("ComputeShader_AverageBuffers");

        averageBuffers.SetFloat("Scalar", 1.0f / samples);
        averageBuffers.SetTexture(ComputeShader_AverageBuffers, "Read", cameraRenderTexture);
        averageBuffers.SetTexture(ComputeShader_AverageBuffers, "Write", computeShaderRenderTexture);

        float customFocalDistance = customFocalPoint != null ? Vector3.Distance(customFocalPoint.position, transform.position) : focalDistance;
        Vector3 focalPoint = transform.position + transform.forward * customFocalDistance;

        if (retainPostProcessing)
        {
            if (postProcessLayer != null)
                postProcessLayer.enabled = false;
        }

        //|||||||||||||||||||||||||||||||||||||||||| CAPTURE ACCUMULATION ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| CAPTURE ACCUMULATION ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| CAPTURE ACCUMULATION ||||||||||||||||||||||||||||||||||||||||||

        for (int i = 0; i < samples; i++)
        {
            Vector2 circleSample = Vector2.zero;

            if(samplingType == SamplingType.Random)
                circleSample = Random.insideUnitCircle;
            else
                circleSample = Hammersley2dSeq((uint)i, (uint)samples) * 2.0f - Vector2.one;

            if (Mathf.Abs(Vector2.Distance(circleSample, Vector2.zero)) > 1.0f)
                continue;

            circleSample *= (cameraComponent.sensorSize * 0.001f) / aperture;
            circleSample.y *= cameraComponent.aspect;

            transform.position = originalCameraPosition;
            transform.rotation = originalCameraRotation;

            Vector3 newCameraPosition = originalCameraPosition + transform.TransformDirection(new Vector3(circleSample.x, circleSample.y, 0));
            Vector3 newCameraDirection = Vector3.Normalize(focalPoint - newCameraPosition);
            Quaternion newCameraRotation = Quaternion.LookRotation(newCameraDirection, Vector3.up);

            transform.position = newCameraPosition;
            transform.rotation = newCameraRotation;

            cameraComponent.Render();

            averageBuffers.Dispatch(ComputeShader_AverageBuffers, Mathf.CeilToInt(resolution.x / THREAD_GROUP_SIZE_X), Mathf.CeilToInt(resolution.y / THREAD_GROUP_SIZE_Y), 1);

            UpdateProgressBar(string.Format("Capturing Image ({0} / {1})", i, samples), 0.5f);
        }

        //|||||||||||||||||||||||||||||||||||||||||| CLEANUP ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| CLEANUP ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| CLEANUP ||||||||||||||||||||||||||||||||||||||||||

        transform.position = originalCameraPosition;
        transform.rotation = originalCameraRotation;

        //|||||||||||||||||||||||||||||||||||||||||| RETAIN POST PROCESSING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| RETAIN POST PROCESSING ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| RETAIN POST PROCESSING ||||||||||||||||||||||||||||||||||||||||||

        if(retainPostProcessing)
        {
            Texture2D convertedRenderTexture = ConvertFromRenderTexture2D(computeShaderRenderTexture, TextureFormat.RGBAFloat);

            if (postProcessLayer != null)
                postProcessLayer.enabled = true;

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            MeshRenderer quadMeshRenderer = quad.GetComponent<MeshRenderer>();
            Material quadMaterial = new Material(Shader.Find("Unlit/Texture"));
            quadMaterial.mainTexture = convertedRenderTexture;
            quadMeshRenderer.sharedMaterial = quadMaterial;

            quad.transform.position = transform.position + transform.forward * (cameraComponent.nearClipPlane + 0.0001f);
            quad.transform.rotation = transform.rotation;
            quad.transform.localScale = new Vector2(cameraComponent.aspect, 1.0f) * cameraComponent.nearClipPlane;

            DepthTextureMode currentMode = cameraComponent.depthTextureMode;

            cameraComponent.depthTextureMode = DepthTextureMode.None;
            cameraComponent.Render();

            cameraComponent.depthTextureMode = currentMode;

            DestroyImmediate(quad);
            DestroyImmediate(quadMaterial);
        }

        cameraComponent.forceIntoRenderTexture = false;
        cameraComponent.targetTexture = null;

        //|||||||||||||||||||||||||||||||||||||||||| CONVERT FINAL IMAGE TO GAMMA ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| CONVERT FINAL IMAGE TO GAMMA ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| CONVERT FINAL IMAGE TO GAMMA ||||||||||||||||||||||||||||||||||||||||||

        Texture2D finalTexture = null;

        if (retainPostProcessing)
            finalTexture = ConvertFromRenderTexture2D(cameraRenderTexture, TextureFormat.RGBAFloat);
        else
            finalTexture = ConvertFromRenderTexture2D(computeShaderRenderTexture, TextureFormat.RGBAFloat);

        UpdateProgressBar("Converting Final Image To Gamma", 0.5f);

        Color[] colors = finalTexture.GetPixels();

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i].r = Mathf.LinearToGammaSpace(colors[i].r);
            colors[i].g = Mathf.LinearToGammaSpace(colors[i].g);
            colors[i].b = Mathf.LinearToGammaSpace(colors[i].b);
            colors[i].a = 1.0f;
        }

        finalTexture.SetPixels(colors);
        finalTexture.Apply();

        //|||||||||||||||||||||||||||||||||||||||||| SAVE IMAGE TO DISK ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| SAVE IMAGE TO DISK ||||||||||||||||||||||||||||||||||||||||||
        //|||||||||||||||||||||||||||||||||||||||||| SAVE IMAGE TO DISK ||||||||||||||||||||||||||||||||||||||||||

        AssetDatabase.DeleteAsset("Assets/OfflineDepthOfField/Render.png");
        System.IO.File.WriteAllBytes(Application.dataPath + "/OfflineDepthOfField/Render.png", ImageConversion.EncodeToPNG(finalTexture));
        AssetDatabase.ImportAsset("Assets/OfflineDepthOfField/Render.png");

        cameraRenderTexture.Release();
        computeShaderRenderTexture.Release();

        CloseProgressBar();
    }

    //|||||||||||||||||||||||||||||||||||||||||| UNITY EDITOR ||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||| UNITY EDITOR ||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||| UNITY EDITOR ||||||||||||||||||||||||||||||||||||||||||

    public void UpdateProgressBar(string description, float progress) => EditorUtility.DisplayProgressBar("Offline Depth Of Field", description, progress);

    public void CloseProgressBar() => EditorUtility.ClearProgressBar();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;

        float currentFocalDistance = customFocalPoint != null ? Vector3.Distance(customFocalPoint.position, transform.position) : focalDistance;
        Vector3 focalPoint = transform.position * currentFocalDistance;

        Gizmos.DrawSphere(focalPoint, gizmoSize);
        Gizmos.DrawLine(transform.position, focalPoint);

        if (showFocalPlane)
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.5f);

            Handles.color = transparentGreen;

            Camera cameraComponent = GetComponent<Camera>();

            float xSize = currentFocalDistance * cameraComponent.rect.width;
            float ySize = currentFocalDistance * cameraComponent.rect.height;

            Vector3[] rectangleVerts = new Vector3[4];
            rectangleVerts[0] = transform.TransformPoint(new Vector3(xSize, ySize, currentFocalDistance));
            rectangleVerts[1] = transform.TransformPoint(new Vector3(xSize, -ySize, currentFocalDistance));
            rectangleVerts[2] = transform.TransformPoint(new Vector3(-xSize, -ySize, currentFocalDistance));
            rectangleVerts[3] = transform.TransformPoint(new Vector3(-xSize, ySize, currentFocalDistance));

            Handles.DrawSolidRectangleWithOutline(rectangleVerts, transparentGreen, Color.green);
        }
    }

    //|||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||| UTILITIES ||||||||||||||||||||||||||||||||||||||||||

    public static Texture2D ConvertFromRenderTexture2D(RenderTexture rt, TextureFormat assetFormat, bool mipChain = false, bool alphaIsTransparency = false)
    {
        Texture2D output = new Texture2D(rt.width, rt.height, assetFormat, mipChain);
        output.alphaIsTransparency = alphaIsTransparency;

        RenderTexture.active = rt;

        output.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        output.Apply();

        rt.Release();

        return output;
    }

    //|||||||||||||||||||||||||||||||||||||||||| SAMPLING ||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||| SAMPLING ||||||||||||||||||||||||||||||||||||||||||
    //|||||||||||||||||||||||||||||||||||||||||| SAMPLING ||||||||||||||||||||||||||||||||||||||||||

    // Ref: http://holger.dammertz.org/stuff/notes_HammersleyOnHemisphere.html
    public static uint ReverseBits32(uint bits)
    {
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x00ff00ff) << 8) | ((bits & 0xff00ff00) >> 8);
        bits = ((bits & 0x0f0f0f0f) << 4) | ((bits & 0xf0f0f0f0) >> 4);
        bits = ((bits & 0x33333333) << 2) | ((bits & 0xcccccccc) >> 2);
        bits = ((bits & 0x55555555) << 1) | ((bits & 0xaaaaaaaa) >> 1);
        return bits;
    }

    public static float VanDerCorputBase2(uint i) => ReverseBits32(i) * 1.0f / 4294967296.0f; // 2^-32

    public static Vector2 Hammersley2dSeq(uint i, uint sequenceLength) => new Vector2((float)i / (float)sequenceLength, VanDerCorputBase2(i));
}
