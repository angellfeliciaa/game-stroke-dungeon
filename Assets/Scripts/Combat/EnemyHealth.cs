using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [Min(1)]
    public int maxHealth = 3;

    [SerializeField]
    private int currentHealth;

    [Header("Event")]
    public UnityEvent onDefeated;

    private Animator animator;
    private bool isDead;
    public bool IsDead => isDead;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        currentHealth = maxHealth;
        isDead = false;
    }

    public void TakeDamage(int damage = 1)
    {
        if (isDead || damage <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);

        Debug.Log(name + " terkena damage. HP tersisa: " + currentHealth);

        if (currentHealth <= 0)
        {
            isDead = true;

            if (animator != null)
                animator.SetTrigger("Die");

            onDefeated?.Invoke();
        }
        else
        {
            if (animator != null)
                animator.SetTrigger("Hit");
        }
    }

    [ContextMenu("Test Take 1 Damage")]
    private void TestTakeDamage()
    {
        TakeDamage(1);
    }
}
