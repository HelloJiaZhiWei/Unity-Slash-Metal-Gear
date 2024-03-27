using UnityEngine;
using DG.Tweening;
using Cinemachine;
using EzySlice;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MGR_BladeMode : MonoBehaviour
{
    public bool bladeMode;

    private Animator anim;
    private MovementInput movement;
    private Vector3 normalOffset;
    public Vector3 zoomOffset;
    private float normalFOV;
    public float zoomFOV = 15;

    public Transform cutPlane;

    public CinemachineFreeLook TPCamera;

    public Material crossMaterial;
    private CinemachineComposer[] composers;

    public LayerMask layerMask;
    ParticleSystem[] particles;
    Volume bladeModeEffect;

    private void Start() 
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = true;
        cutPlane.gameObject.SetActive(false);
        
        anim = GetComponent<Animator>();
        movement = GetComponent<MovementInput>();
        normalFOV = TPCamera.m_Lens.FieldOfView; 
        composers = new CinemachineComposer[3];
        for(int i = 0;i<3;i++)
        {
            composers[i] = TPCamera.GetRig(i).GetCinemachineComponent<CinemachineComposer>();
        }
        normalOffset = composers[0].m_TrackedObjectOffset;
        
        particles = cutPlane.GetComponentsInChildren<ParticleSystem>();
        bladeModeEffect = Camera.main.GetComponent<Volume>();
    }

    private void Update() 
    {
        anim.SetFloat("x",Mathf.Clamp(Camera.main.transform.GetChild(0).localPosition.x+0.3f,-1,1));
        anim.SetFloat("y",Mathf.Clamp(Camera.main.transform.GetChild(0).localPosition.y+0.18f,-1,1));

        if (Input.GetMouseButtonDown(1))
        {
            Zoom(true);
        }

        if (Input.GetMouseButtonUp(1))
        {
            Zoom(false);
        }

        if(bladeMode)
        {
            transform.DORotate(Camera.main.transform.rotation.eulerAngles,.2f);
            RotatePlane();
            if (Input.GetMouseButtonDown(0))
            {
                cutPlane.GetChild(0).DOComplete();
                cutPlane.GetChild(0).DOLocalMoveX(-cutPlane.GetChild(0).transform.localPosition.x,0.1f).SetEase(Ease.OutExpo);
                ShakeCamera();
                Slice();
            }
        }
    }

    void Zoom(bool on)
    {
        if(on)
        {
            cutPlane.gameObject.SetActive(true);
            bladeMode = true;
        }
        else
        {
            cutPlane.gameObject.SetActive(false);
            bladeMode = false;

            transform.DORotate(new Vector3(0,transform.eulerAngles.y,0),0.2f);
        }
        anim.SetBool("bladeMode", bladeMode);

        string x = on ? "Horizontal" : "Mouse X";
        string y = on ? "Vertical" : "Mouse Y";
        TPCamera.m_XAxis.m_InputAxisName = x;
        TPCamera.m_YAxis.m_InputAxisName = y;

        float fov = on ? zoomFOV : normalFOV;
        Vector3 offset = on ? zoomOffset : normalOffset;
        float timeScale = on ? .2f : 1;
        DOVirtual.Float(TPCamera.m_Lens.FieldOfView,fov,-.1f,FieldOfView);
        DOVirtual.Float(composers[0].m_TrackedObjectOffset.x,offset.x,0.2f,CameraOffset).SetUpdate(true);
        DOVirtual.Float(Time.timeScale, timeScale, .02f, SetTimeScale);

        movement.enabled = !on;

        float vig = on ? .6f : 0;
        float chrom = on ? 1 : 0;
        float depth = on ? 4.8f : 8;
        float vig2 = on ? 0f : .6f;
        float chrom2 = on ? 0 : 1;
        float depth2 = on ? 8 : 4.8f;
        DOVirtual.Float(chrom2, chrom, .1f, Chromatic);
        DOVirtual.Float(vig2, vig, .1f, Vignette);
        DOVirtual.Float(depth2, depth, .1f, DepthOfField);
    }

    void RotatePlane()
    {
        cutPlane.eulerAngles += new Vector3(0,0,-Input.GetAxis("Mouse X") * 5f);
    }
    void ShakeCamera()
    {
        TPCamera.GetComponent<CinemachineImpulseSource>().GenerateImpulse();
        foreach (ParticleSystem p in particles)
        {
            p.Play();
        }
    }
    void Slice()
    {
        Collider[] hit = Physics.OverlapBox(cutPlane.position,new Vector3(5f,0.1f,5f),cutPlane.rotation,layerMask);

        if(hit.Length <= 0) return;

        for(int i = 0;i<hit.Length;i++)
        {
            SlicedHull hull = SliceObject(hit[i].gameObject,crossMaterial);
            if(hull != null)
            {
                GameObject upper = hull.CreateUpperHull(hit[i].gameObject, crossMaterial);
                GameObject bottom = hull.CreateLowerHull(hit[i].gameObject, crossMaterial);
                AddHullComponents(upper);
                AddHullComponents(bottom);
                Destroy(hit[i].gameObject);
            }
        }
    }
    SlicedHull SliceObject(GameObject obj,Material crossSectionMaterial = null)
    {
        if(obj.GetComponent<MeshFilter>() == null) return null;
        return obj.Slice(cutPlane.position,cutPlane.up,crossMaterial);
    }

    void AddHullComponents(GameObject obj)
    {
        obj.layer = 7;
        Rigidbody rb = obj.AddComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        MeshCollider collider = obj.AddComponent<MeshCollider>();
        collider.convex = true;
        rb.AddExplosionForce(100,obj.transform.position,20);
    }

    void CameraOffset(float x)
    {
        foreach(CinemachineComposer p in composers)
        {
            p.m_TrackedObjectOffset.Set(x,p.m_TrackedObjectOffset.y,p.m_TrackedObjectOffset.z);
        }
    }
    void FieldOfView(float fov)
    {
        TPCamera.m_Lens.FieldOfView = fov;
    }
    void SetTimeScale(float timeScale)
    {
        Time.timeScale = timeScale;
    }

    void Chromatic(float x)
    {
        ChromaticAberration chromaticAberration;
        bladeModeEffect.profile.TryGet<ChromaticAberration>(out chromaticAberration);
        chromaticAberration.intensity.value = x;
    }

    void Vignette(float x)
    {
        Vignette vignette;
        bladeModeEffect.profile.TryGet<Vignette>(out vignette);
        vignette.intensity.value = x;
    }

    void DepthOfField(float x)
    {
        DepthOfField depthOfField;
        bladeModeEffect.profile.TryGet<DepthOfField>(out depthOfField);
        depthOfField.aperture.value = x;
    }
}
                          