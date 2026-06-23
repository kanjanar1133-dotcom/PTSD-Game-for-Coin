using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/*
 * วิธีใช้งาน FadeManager (How to use FadeManager):
 * 1. สร้าง Empty GameObject เปล่าขึ้นมาในฉากแรกของเกม แล้วตั้งชื่อว่า "FadeManager"
 * 2. ลาก FadeManager.cs ไปแปะใส่ GameObject นั้น
 * 3. ที่ Hierarchy ฝั่งซ้ายหน้าจอ คลิกขวา เลือก UI > Canvas (อาจจะตั้ง Canvas Sort Order ไป 99 ให้มันอยู่บนสุดตลอด)
 * 4. คลิกขวาใน Canvas นั้น เลือก UI > Image จะได้รูปสีขาวมา
 * 5. ปรับสี Image นั้น เป็น "สีดำทึบ" และปรับขนาดให้เต็มจอ (Anchor = Stretch มุมขวาล่างสุด)
 * 6. กลับไปที่ GameObject "FadeManager" ให้ดูใน Inspector 
 * 7. ลาก Image สีดำจากข้อ (4) ไปหยอดใส่ช่องลูกศร "Fade Image" จบสิ้นขั้นตอนเซ็ตอัพ!
 */

public class FadeManager : MonoBehaviour
{
    // โครงสร้างแบบ Singleton ทำให้สามารถดึงโค้ดตัวนี้ไปใช้ได้จากทุกที่ตลอดเวลา โดยอ้างอิงผ่านคำว่า FadeManager.Instance
    public static FadeManager Instance;

    [Header("UI References")]
    [Tooltip("ใส่ Image สีดำที่ปรับ Alpha ขยายเต็มจอ (Anchor = Stretch)")]
    [SerializeField] private Image fadeImage;
    
    [Header("Fade Settings")]
    [Tooltip("ระยะเวลาในการ Fade (วินาที)")]
    [SerializeField] private float fadeDuration = 1f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // ทำให้ Manager อยู่ย้ายข้ามฉากได้โดยไม่ถูกทำลาย (จะไม่พังเวลาเปลี่ยน Scene)
            DontDestroyOnLoad(gameObject); 
            Debug.Log("[Fade Debug] FadeManager ถูกสร้างขึ้นแล้วและเตรียมพร้อมใช้งาน!");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // เริ่มเกมมาให้อยู่ในสถานะใสเสมอ (ถ้าต้องการให้เริ่มต้นมามืดแล้วสว่าง ให้ใช้ FadeToClear() แทน)
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = 0f;
            fadeImage.color = c;
            fadeImage.raycastTarget = false; // ปิดการกันกดปุ่มต่างๆ
        }
        else 
        {
            Debug.LogWarning("[Fade Debug Warning] ลืมใส่ Fade Image สีดำ ใน FadeManager หรือเปล่า?");
        }
    }

    /// <summary>
    /// สั่ง Fade ให้ภาพค่อยๆ มืดสนิท (Alpha 0 -> 1)
    /// </summary>
    public void FadeToBlack()
    {
        Debug.Log("[Fade Debug] มีคำสั่งเรียก FadeToBlack จอกำลังจะมืดลง...");
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(1f));
    }

    /// <summary>
    /// สั่ง Fade ให้ภาพค่อยๆ สว่างใส (Alpha 1 -> 0)
    /// </summary>
    public void FadeToClear()
    {
        Debug.Log("[Fade Debug] มีคำสั่งเรียก FadeToClear จอกำลังจะกลับมาสว่างใส...");
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(0f));
    }

    /// <summary>
    /// แบบสำเร็จรูป: Fade มืด -> โหลดฉากข้าม Scene -> Fade สว่าง (สำหรับการเข้าประตูข้ามไปยังฉากคนละชื่อ)
    /// </summary>
    /// <param name="sceneName">ชื่อฉากที่จะโหลด</param>
    public void FadeAndLoadScene(string sceneName)
    {
        Debug.Log($"[Fade Debug] มีคำสั่งให้โหลดฉากใหม่: {sceneName} โดยกำลังทำจอมืด...");
        StartCoroutine(FadeAndLoadRoutine(sceneName));
    }

    /// <summary>
    /// แบบประยุกต์: Fade มืด -> ทำคำสั่งบางอย่าง (เช่น ย้ายจุดผู้เล่นข้ามห้องในฉากเดิม) -> Fade สว่าง
    /// </summary>
    /// <param name="action">คำสั่ง Action โค้ดที่ต้องการให้รันทำงานตอนช่วงที่จอมืดสนิทที่สุด</param>
    public void FadeAndExecute(System.Action action)
    {
        StartCoroutine(FadeAndExecuteRoutine(action));
    }

    // Coroutine กลางสำหรับควบคุม Alpha (ความใส/ทึบ) ตัวแม่ 
    private IEnumerator FadeRoutine(float targetAlpha)
    {
        if (fadeImage == null) yield break;

        fadeImage.raycastTarget = true; // เปิดไว้ เผื่อดักการกดสุ่มสี่สุ่มห้าระหว่างทำ Fade
        Color currentColor = fadeImage.color;
        float startAlpha = currentColor.a;
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            // Lerp คือการไล่ระดับอย่างซอฟท์นุ่มนวล จาก Alpha เดิม ไป Alpha ใหม่
            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
            fadeImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, newAlpha);
            yield return null;
        }

        // เซ็ตค่าให้ชัวร์ว่าตรงเผง เมื่อจบการ Loop
        fadeImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, targetAlpha);
        
        // ถ้าใส (Alpha 0) คืนค่าให้เรเข้าทะลุได้ เพื่อให้ผู้เล่นใช้เมาส์กด UI ปุ่มคลิกต่างๆ ได้ตามปกติ
        if (targetAlpha <= 0f)
        {
            fadeImage.raycastTarget = false;
        }
    }

    // Coroutine สำหรับข้าม Scene พร้อมภาพตัดดำ
    private IEnumerator FadeAndLoadRoutine(string sceneName)
    {
        // 1. Fade ไปมืด (1)
        yield return StartCoroutine(FadeRoutine(1f));

        Debug.Log($"[Fade Debug] จอมืดสนิทแล้ว -> กำลังเริ่มโหลด Scene: {sceneName}...");

        // 2. โหลดฉากเตรียมข้ามประตูแบบ Async
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
        {
            yield return null;
        }

        // รอให้ของในฉากใหม่โหลดเสร็จสมบูรณ์จริงๆ 1 เฟรม ป้องกันบั๊กจอดำค้าง
        yield return null;

        Debug.Log($"[Fade Debug] โหลด Scene: {sceneName} เสร็จสิ้น -> กำลังเปิดภาพสว่างขึ้น...");

        // 3. ฉากใหม่โหลดเสร็จ ให้ค่อยๆ Fade สว่าง (0) 
        yield return StartCoroutine(FadeRoutine(0f));
    }

    // Coroutine สำหรับกระชากย้ายตำแหน่งผู้เล่น หรือ รันจังหวะ Event ของเกมพร้อมภาพตัดดำ
    private IEnumerator FadeAndExecuteRoutine(System.Action action)
    {
        // 1. Fade โลกให้มืดสนิท
        yield return StartCoroutine(FadeRoutine(1f));

        // 2. สั่งรันคำสั่งพิเศษ (เช่นคำสั่งดึงตัวผู้เล่นย้ายไปอีกที่ในพื้นที่แมพ)
        action?.Invoke();

        // หน่วงเวลา 0.2 วิ ให้ผู้เล่นไม่มึนหัวเล็กน้อย
        yield return new WaitForSeconds(0.2f);

        // 3. ปรับ Alpha อีกครั้งให้สว่างสเปซเต็มที่
        yield return StartCoroutine(FadeRoutine(0f));
    }
}
