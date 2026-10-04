using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Level1Manager : MonoBehaviour
{
    [Header("Karakter")]
    public Transform karakterUtama;
    public Animator animKarakter;

    [Header("Arena 1")]
    public Transform attackPointSkeleton1;
    public EnemyHealth enemySkeleton1;

    [Header("Pintu Keluar Arena 1")]
    public Transform pointBeforeDoorRoom1;
    public Transform pointAfterDoorRoom1;
    public Animator animPintuRoom1;

    [Header("Masuk Arena 2")]
    public Transform pointBeforeSideDoorRoom2;
    public Transform pointAfterSideDoorRoom2;
    public Animator animSideDoorRoom2;

    [Header("Dua Enemy Arena 2")]
    public EnemyHealth enemy2A;
    public Transform attackPointEnemy2A;
    public EnemyHealth enemy2B;
    public Transform attackPointEnemy2B;

    [Header("Pintu Keluar Arena 2")]
    public Transform pointBeforeDoorRoom2;
    public Transform pointAfterDoorRoom2;
    public Animator animPintuRoom2;

    [Header("Tempat Istirahat")]
    public Transform pointBelokIstirahat;
    public Transform pointIstirahat;
    public GameObject panelIstirahat;

    [Header("Masuk Arena 3")]
    public Transform pointBelokKiriArena3;
    public Transform pointDepanPintuArena3;
    public Transform pointSetelahPintuArena3;
    public Animator animSideDoorArena3;

    [Header("Vampire Arena 3")]
    public EnemyHealth enemyVampireArena3;
    public Transform attackPointVampirePhase1;
    public Transform pointVampirePhase2;
    public Transform attackPointVampirePhase2;
    public float durasiHilangVampire = 0.45f;

    [Header("Pintu Keluar Arena 3")]
    public Transform pointBeforeDoorRoom3;
    public Transform pointAfterDoorRoom3;
    public Animator animPintuRoom3;

    [Header("Akhir Level 1")]
    public GameObject panelLevelSelesai;
    public GameObject panelRingkasan;
    [Min(0f)] public float durasiPanelSelesai = 3f;
    public string namaSceneMenuUtama = "SampleScene";
    public string namaSceneLevel2 = "Level2";

    [Header("Dialog Singkat")]
    public GameObject panelDialog;

    [Header("Gerakan")]
    public float kecepatanJalan = 2.5f;
    public float jarakBerhenti = 0.05f;

    [Header("Serangan MC")]
    public float jarakSerangMaksimal = 1.3f;
    public float jedaDamage = 0.25f;
    public float durasiAttack = 0.6f;

    [Header("Balasan Visual Enemy")]
    public float jedaSebelumSkeletonMenyerang = 0.25f;
    public float durasiSeranganSkeleton = 0.7f;

    [Header("Jeda Animasi dan Pintu")]
    public float durasiAnimasiDeath = 1f;
    public float waktuPintuTerbuka = 0.8f;

    public bool siapMenyerang { get; private set; }

    public bool sedangIstirahat { get; private set; }
    public EnemyHealth ActiveEnemy => enemyAktif;
    public bool IsLevelComplete => level1Selesai;
    public bool IsAttacking => sedangMenyerang;
    public bool CameraSessionExpected => cameraSessionDetected;

    private bool sedangMenyerang;
    private bool lanjutDariIstirahat;
    private EnemyHealth enemyAktif;
    private string triggerSeranganAktif;
    private int jumlahHitVampire;
    private bool vampireAkanBerpindah;
    private bool level1Selesai;
    private TMP_Text namaPembicara;
    private TMP_Text isiDialog;
    private Image gambarPembicara;
    private Sprite potretJoshua;
    private int versiDialog;
    private Level1Hud hud;
    private Level1Audio levelAudio;
    private UDPReceiver gestureReceiver;
    private int lastSqueezeVersion;
    private bool cameraSessionDetected;

    private void Start()
    {
        hud = GetComponent<Level1Hud>();
        levelAudio = GetComponentInChildren<Level1Audio>();
        gestureReceiver = UDPReceiver.Instance;
        cameraSessionDetected = gestureReceiver != null;
        if (gestureReceiver != null)
            lastSqueezeVersion = gestureReceiver.SqueezeVersion;
        if (panelIstirahat != null)
            panelIstirahat.SetActive(false);

        if (panelLevelSelesai != null)
            panelLevelSelesai.SetActive(false);

        if (panelRingkasan != null)
            panelRingkasan.SetActive(false);

        if (panelDialog != null)
        {
            namaPembicara = panelDialog.transform.Find("Name_Text")?.GetComponent<TMP_Text>();
            isiDialog = panelDialog.transform.Find("Dialog_Text")?.GetComponent<TMP_Text>();
            gambarPembicara = panelDialog.transform.Find("Potrait_Box/Face_Sprite")?.GetComponent<Image>();
            if (gambarPembicara != null)
            {
                potretJoshua = gambarPembicara.sprite;
                gambarPembicara.preserveAspect = true;
            }
            panelDialog.SetActive(false);
        }

        if (karakterUtama == null ||
            animKarakter == null ||
            attackPointSkeleton1 == null ||
            enemySkeleton1 == null)
        {
            Debug.LogError(
                "Referensi karakter atau Arena 1 belum lengkap di Level1Manager."
            );
            return;
        }

        StartCoroutine(AlurLevel1());
    }

    private void Update()
    {
        bool mouseClick = Input.GetMouseButtonDown(0);
        // Consume camera events even while walking or paused so an earlier
        // squeeze cannot trigger the next encounter or leave the rest area.
        bool actionRequested = ReadActionInput(mouseClick);

        if (hud != null &&
            (hud.IsPaused ||
             (mouseClick && hud.IsPointerOverControl)))
            return;

        if (sedangIstirahat)
        {
            if (actionRequested)
                lanjutDariIstirahat = true;

            return;
        }

        if (!siapMenyerang ||
            sedangMenyerang ||
            enemyAktif == null ||
            enemyAktif.IsDead)
        {
            return;
        }

        if (actionRequested)
        {
            StartCoroutine(
                SerangEnemy(enemyAktif, triggerSeranganAktif)
            );
        }
    }

    private bool ReadActionInput(bool mouseClick)
    {
        UDPReceiver receiver = UDPReceiver.Instance;
        if (receiver != gestureReceiver)
        {
            gestureReceiver = receiver;
            if (receiver != null)
            {
                cameraSessionDetected = true;
                lastSqueezeVersion = receiver.SqueezeVersion;
            }
        }

        if (receiver != null)
        {
            int currentVersion = receiver.SqueezeVersion;
            bool newSqueeze = currentVersion != lastSqueezeVersion;
            lastSqueezeVersion = currentVersion;
            // The receiver may process several camera packets in one frame.
            // The fist event is already validated there; a following uncertain
            // packet must not erase the attack before this Update runs.
            return newSqueeze && receiver.ConnectionError == null &&
                receiver.IsListening && receiver.IsCalibrationCompleted &&
                receiver.HasFreshData;
        }

        // Direct Level1 editor testing remains possible without SampleScene.
        // Once a camera session has existed, a disconnect never enables mouse.
        return !cameraSessionDetected && mouseClick;
    }

    private IEnumerator AlurLevel1()
    {
        // ===== ARENA 1 =====
        StartCoroutine(TampilkanDialog("Joshua", "One room at a time. Breathe, then move.", 3f));
        yield return StartCoroutine(
            Bertarung(
                enemySkeleton1,
                attackPointSkeleton1,
                "Attack",
                false
            )
        );

        if (pointBeforeDoorRoom1 == null ||
            pointAfterDoorRoom1 == null ||
            animPintuRoom1 == null)
        {
            Debug.LogError(
                "Titik atau pintu keluar Arena 1 belum diisi."
            );
            yield break;
        }

        yield return StartCoroutine(
            JalanKeTitik(pointBeforeDoorRoom1)
        );

        BerhentiMenghadapAtas();
        animPintuRoom1.SetTrigger("Open");
        levelAudio?.PlayDoor();

        yield return new WaitForSeconds(waktuPintuTerbuka);

        yield return StartCoroutine(
            JalanKeTitik(pointAfterDoorRoom1)
        );

        StartCoroutine(TampilkanDialog("Joshua", "The next room is close. Keep going.", 3f));

        // ===== MASUK ARENA 2 =====
        if (pointBeforeSideDoorRoom2 == null ||
            pointAfterSideDoorRoom2 == null ||
            animSideDoorRoom2 == null ||
            enemy2A == null ||
            attackPointEnemy2A == null ||
            enemy2B == null ||
            attackPointEnemy2B == null)
        {
            Debug.LogError(
                "Referensi Arena 2 belum lengkap di Level1Manager."
            );
            yield break;
        }

        yield return StartCoroutine(
            JalanKeTitik(pointBeforeSideDoorRoom2)
        );

        animSideDoorRoom2.SetTrigger("Open");
        levelAudio?.PlayDoor();

        yield return new WaitForSeconds(waktuPintuTerbuka);

        yield return StartCoroutine(
            JalanKeTitik(pointAfterSideDoorRoom2)
        );

        // Lawan enemy pertama.
        yield return StartCoroutine(
            Bertarung(
                enemy2A,
                attackPointEnemy2A,
                "AttackRight",
                true
            )
        );

        // Setelah enemy pertama mati, lawan enemy kedua.
        yield return StartCoroutine(
            Bertarung(
                enemy2B,
                attackPointEnemy2B,
                "AttackRight",
                true
            )
        );

        Debug.Log("Kedua enemy Arena 2 telah dikalahkan.");

        // ===== KELUAR ARENA 2 =====
        if (pointBeforeDoorRoom2 == null ||
            pointAfterDoorRoom2 == null ||
            animPintuRoom2 == null)
        {
            Debug.LogError(
                "Titik atau pintu keluar Arena 2 belum diisi."
            );
            yield break;
        }

        yield return StartCoroutine(
            JalanKeTitik(pointBeforeDoorRoom2)
        );

        BerhentiMenghadapAtas();
        animPintuRoom2.SetTrigger("Open");
        levelAudio?.PlayDoor();

        yield return new WaitForSeconds(waktuPintuTerbuka);

        yield return StartCoroutine(
            JalanKeTitik(pointAfterDoorRoom2)
        );

        BerhentiMenghadapAtas();

        Debug.Log("MC sudah keluar dari Arena 2.");

        StartCoroutine(TampilkanDialog("Joshua", "A light ahead... I can catch my breath.", 3f));

        // ===== TEMPAT ISTIRAHAT =====
        if (pointBelokIstirahat == null ||
            pointIstirahat == null ||
            panelIstirahat == null)
        {
            Debug.LogError(
                "Titik atau panel Tempat Istirahat belum diisi di Level1Manager."
            );
            yield break;
        }

        yield return StartCoroutine(
            JalanKeTitik(pointBelokIstirahat)
        );

        yield return StartCoroutine(
            JalanKeTitik(pointIstirahat)
        );

        // Hadapkan MC ke kristal di sebelah kanannya saat berhenti.
        BerhentiMenghadapKanan();

        siapMenyerang = false;
        lanjutDariIstirahat = false;
        panelIstirahat.SetActive(true);
        levelAudio?.EnterRest();

        // Tunggu satu frame agar klik dari langkah sebelumnya tidak terbaca.
        yield return null;
        sedangIstirahat = true;

        Debug.Log("MC tiba di Tempat Istirahat. Squeeze once when ready to continue.");

        yield return new WaitUntil(() => lanjutDariIstirahat);

        sedangIstirahat = false;
        panelIstirahat.SetActive(false);
        levelAudio?.LeaveRest();
        Debug.Log("Istirahat selesai. MC menuju Arena 3.");
        StartCoroutine(TampilkanDialog("Joshua", "The air is colder beyond this door.", 3f));

        // ===== MASUK ARENA 3 =====
        if (pointBelokKiriArena3 == null ||
            pointDepanPintuArena3 == null ||
            pointSetelahPintuArena3 == null ||
            animSideDoorArena3 == null)
        {
            Debug.LogError(
                "Waypoint atau pintu Arena 3 belum diisi di Level1Manager."
            );
            yield break;
        }

        // Kembali dari ceruk kristal, naik, lalu belok kiri di koridor.
        yield return StartCoroutine(
            JalanKeTitik(pointBelokIstirahat)
        );

        yield return StartCoroutine(
            JalanKeTitik(pointBelokKiriArena3)
        );

        yield return StartCoroutine(
            JalanKeTitik(pointDepanPintuArena3)
        );

        animSideDoorArena3.SetTrigger("Open");
        levelAudio?.PlayDoor();
        yield return new WaitForSeconds(waktuPintuTerbuka);

        yield return StartCoroutine(
            JalanKeTitik(pointSetelahPintuArena3)
        );

        Debug.Log("MC sudah masuk Arena 3.");

        if (enemyVampireArena3 == null ||
            attackPointVampirePhase1 == null ||
            pointVampirePhase2 == null ||
            attackPointVampirePhase2 == null)
        {
            Debug.LogError(
                "Referensi pertarungan Vampire Arena 3 belum lengkap di Level1Manager."
            );
            yield break;
        }

        yield return StartCoroutine(BertarungVampire());
        Debug.Log("Vampire Arena 3 telah dikalahkan.");
        StartCoroutine(TampilkanDialog("Joshua", "It's over. The way forward is clear.", 3f));

        // ===== KELUAR ARENA 3 =====
        if (pointBeforeDoorRoom3 == null ||
            pointAfterDoorRoom3 == null ||
            animPintuRoom3 == null)
        {
            Debug.LogError(
                "Titik atau pintu keluar Arena 3 belum diisi di Level1Manager."
            );
            yield break;
        }

        yield return StartCoroutine(JalanKeTitik(pointBeforeDoorRoom3));
        BerhentiMenghadapAtas();

        animPintuRoom3.SetTrigger("Open");
        levelAudio?.PlayDoor();
        yield return new WaitForSeconds(waktuPintuTerbuka);

        yield return StartCoroutine(JalanKeTitik(pointAfterDoorRoom3));
        BerhentiMenghadapAtas();

        if (panelLevelSelesai != null)
            panelLevelSelesai.SetActive(true);
        levelAudio?.PlayVictory();

        level1Selesai = true;
        Debug.Log("LEVEL 1 COMPLETE");

        // Jika panel ringkasan belum dibuat, pertahankan panel kemenangan.
        if (panelRingkasan != null)
        {
            yield return new WaitForSeconds(durasiPanelSelesai);

            if (panelLevelSelesai != null)
                panelLevelSelesai.SetActive(false);

            panelRingkasan.SetActive(true);
        }
    }

    public void KembaliKeMenuUtama()
    {
        if (!level1Selesai)
            return;

        if (!Application.CanStreamedLevelBeLoaded(namaSceneMenuUtama))
        {
            Debug.LogError("Scene menu utama belum ada di Build Settings: " + namaSceneMenuUtama);
            return;
        }

        SceneManager.LoadScene(namaSceneMenuUtama);
    }

    public void LanjutKeLevel2()
    {
        if (!level1Selesai)
            return;

        if (!Application.CanStreamedLevelBeLoaded(namaSceneLevel2))
        {
            Debug.LogWarning("Scene Level 2 belum tersedia: " + namaSceneLevel2);
            return;
        }

        SceneManager.LoadScene(namaSceneLevel2);
    }

    private IEnumerator BertarungVampire()
    {
        siapMenyerang = false;
        enemyAktif = null;
        jumlahHitVampire = 0;
        vampireAkanBerpindah = false;

        // Fase 1: MC berhenti di bawah vampire dan menyerang ke atas.
        yield return StartCoroutine(JalanKeTitik(attackPointVampirePhase1));
        BerhentiMenghadapAtas();
        yield return StartCoroutine(TampilkanDialog("Vampire", "The crystal could not save you.", 2.4f, enemyVampireArena3));
        yield return StartCoroutine(TampilkanDialog("Joshua", "I am still standing.", 2f));
        enemyAktif = enemyVampireArena3;
        triggerSeranganAktif = "Attack";
        siapMenyerang = true;

        Debug.Log("Vampire fase 1: squeeze to attack.");

        yield return new WaitUntil(
            () => vampireAkanBerpindah || enemyVampireArena3.IsDead
        );

        siapMenyerang = false;
        enemyAktif = null;
        yield return new WaitUntil(() => !sedangMenyerang);

        if (!enemyVampireArena3.IsDead)
        {
            // Setelah dua pukulan, vampire menghilang sebentar lalu muncul
            // di posisi kedua. Input serangan ditahan selama perpindahan.
            SpriteRenderer spriteVampire =
                enemyVampireArena3.GetComponent<SpriteRenderer>();

            if (spriteVampire != null)
                spriteVampire.enabled = false;

            levelAudio?.PlayMagic();

            yield return new WaitForSeconds(durasiHilangVampire);
            enemyVampireArena3.transform.position = pointVampirePhase2.position;

            if (spriteVampire != null)
                spriteVampire.enabled = true;

            vampireAkanBerpindah = false;
            StartCoroutine(TampilkanDialog("Vampire", "Can you follow me now?", 2.5f, enemyVampireArena3));

            // Fase 2: MC berjalan otomatis ke titik serang yang baru.
            yield return StartCoroutine(JalanKeTitik(attackPointVampirePhase2));
            BerhentiMenghadapAtas();
            enemyAktif = enemyVampireArena3;
            siapMenyerang = true;

            Debug.Log("Vampire fase 2: squeeze to attack.");
            yield return new WaitUntil(() => enemyVampireArena3.IsDead);

            siapMenyerang = false;
            enemyAktif = null;
            yield return new WaitUntil(() => !sedangMenyerang);
        }

        yield return new WaitForSeconds(durasiAnimasiDeath);
    }

    private IEnumerator Bertarung(
        EnemyHealth enemy,
        Transform titikSerang,
        string triggerSerangan,
        bool menghadapKanan)
    {
        siapMenyerang = false;
        enemyAktif = null;

        yield return StartCoroutine(
            JalanKeTitik(titikSerang)
        );

        if (menghadapKanan)
            BerhentiMenghadapKanan();
        else
            BerhentiMenghadapAtas();

        if (enemy == enemySkeleton1)
        {
            yield return StartCoroutine(TampilkanDialog("Skeleton", "This passage is mine.", 2.2f, enemySkeleton1));
            yield return StartCoroutine(TampilkanDialog("Joshua", "Then I will make my way through.", 2.2f));
        }
        else if (enemy == enemy2A)
        {
            yield return StartCoroutine(TampilkanDialog("Skeleton", "You face us both?", 2.2f, enemy2A));
            yield return StartCoroutine(TampilkanDialog("Joshua", "One at a time.", 1.8f));
        }
        else if (enemy == enemy2B)
        {
            yield return StartCoroutine(TampilkanDialog("Skeleton", "Now face me.", 1.8f, enemy2B));
        }

        enemyAktif = enemy;
        triggerSeranganAktif = triggerSerangan;
        siapMenyerang = true;

        Debug.Log("Siap melawan " + enemy.name);

        yield return new WaitUntil(() => enemy.IsDead);

        siapMenyerang = false;
        enemyAktif = null;

        // Biarkan animasi kematian terlihat.
        yield return new WaitForSeconds(durasiAnimasiDeath);
    }

    private IEnumerator SerangEnemy(
        EnemyHealth target,
        string triggerSerangan)
    {
        float jarak = Vector2.Distance(
            karakterUtama.position,
            target.transform.position
        );

        if (jarak > jarakSerangMaksimal)
        {
            Debug.LogWarning(
                target.name + " terlalu jauh. Jarak: " + jarak
            );
            yield break;
        }

        sedangMenyerang = true;
        animKarakter.SetTrigger(triggerSerangan);
        levelAudio?.PlayPlayerAttack();

        yield return new WaitForSeconds(jedaDamage);

        jarak = Vector2.Distance(
            karakterUtama.position,
            target.transform.position
        );

        if (!target.IsDead && jarak <= jarakSerangMaksimal)
        {
            target.TakeDamage(1);
            levelAudio?.PlayEnemyHit(target.IsDead);

            if (target == enemyVampireArena3)
            {
                jumlahHitVampire++;

                if (jumlahHitVampire == 2 && !target.IsDead)
                {
                    vampireAkanBerpindah = true;
                    siapMenyerang = false;
                }
            }
        }

        yield return new WaitForSeconds(
            Mathf.Max(0f, durasiAttack - jedaDamage)
        );

        if (!target.IsDead && !vampireAkanBerpindah)
        {
            yield return new WaitForSeconds(
                jedaSebelumSkeletonMenyerang
            );

            Animator animEnemy = target.GetComponent<Animator>();

            if (animEnemy != null)
                animEnemy.SetTrigger("Attack");
            levelAudio?.PlayEnemyAttack();

            yield return new WaitForSeconds(
                durasiSeranganSkeleton
            );
        }

        sedangMenyerang = false;
    }

    private IEnumerator JalanKeTitik(Transform tujuan)
    {
        while (Vector2.Distance(
                   karakterUtama.position,
                   tujuan.position) > jarakBerhenti)
        {
            SetAnimasiJalan(tujuan.position);
            levelAudio?.PlayFootstepIfDue();

            karakterUtama.position = Vector3.MoveTowards(
                karakterUtama.position,
                tujuan.position,
                kecepatanJalan * Time.deltaTime
            );

            yield return null;
        }

        karakterUtama.position = tujuan.position;
        animKarakter.SetFloat("Speed", 0f);
    }

    private IEnumerator TampilkanDialog(string pembicara, string kalimat, float durasi, EnemyHealth karakterEnemy = null)
    {
        if (panelDialog == null || namaPembicara == null || isiDialog == null)
            yield break;

        int versiIni = ++versiDialog;
        namaPembicara.text = pembicara;
        isiDialog.text = kalimat;
        if (gambarPembicara != null)
        {
            Sprite potret = potretJoshua;
            if (karakterEnemy != null)
            {
                SpriteRenderer rendererEnemy = karakterEnemy.GetComponentInChildren<SpriteRenderer>();
                if (rendererEnemy != null && rendererEnemy.sprite != null)
                    potret = rendererEnemy.sprite;
            }
            gambarPembicara.sprite = potret;
        }
        panelDialog.SetActive(true);

        yield return new WaitForSeconds(durasi);

        // Dialog lama tidak boleh menutup dialog yang lebih baru.
        if (versiIni == versiDialog)
            panelDialog.SetActive(false);
    }

    private void BerhentiMenghadapAtas()
    {
        animKarakter.SetFloat("MoveX", 0f);
        animKarakter.SetFloat("MoveY", 1f);
        animKarakter.SetFloat("Speed", 0f);
    }

    private void BerhentiMenghadapKanan()
    {
        animKarakter.SetFloat("MoveX", 1f);
        animKarakter.SetFloat("MoveY", 0f);
        animKarakter.SetFloat("Speed", 0f);

        int idleKanan = Animator.StringToHash(
            "Base Layer.Char_Idle_Right"
        );

        if (animKarakter.HasState(0, idleKanan))
        {
            animKarakter.Play(idleKanan, 0, 0f);
        }
    }

    private void SetAnimasiJalan(Vector3 target)
    {
        Vector3 arah = (
            target - karakterUtama.position
        ).normalized;

        if (Mathf.Abs(arah.x) > Mathf.Abs(arah.y))
        {
            animKarakter.SetFloat(
                "MoveX",
                arah.x > 0 ? 1f : -1f
            );
            animKarakter.SetFloat("MoveY", 0f);
        }
        else
        {
            animKarakter.SetFloat("MoveX", 0f);
            animKarakter.SetFloat(
                "MoveY",
                arah.y > 0 ? 1f : -1f
            );
        }

        animKarakter.SetFloat("Speed", 1f);
    }
}
