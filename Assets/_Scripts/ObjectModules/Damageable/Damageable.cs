using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ElementType { Physical, Siphon, Fire, Ice, Shock, Poison }

public class Damageable : ObjectModule {

    public event System.Action OnModuleInit;

    [SerializeField] protected HealthAttributes defaultHPAttributes;
    [SerializeField] protected IFrameProperties iFrameProperties;

    public int Health => runtimeHP != null ? runtimeHP.Health
                                           : -1;
    public int MaxHealth => runtimeHP != null ? runtimeHP.MaxHealth
                                              : -1;

    protected RuntimeHealthAttributes runtimeHP;

    protected bool localIFrameOn, externalIFrameOn;
    protected bool IFrameOn => localIFrameOn || externalIFrameOn;

    protected virtual void Awake() {
        baseObject.UpdateRendererRefs();
        baseObject.OnTryDamage += BaseObject_OnTryDamage;
        baseObject.OnTryHeal += BaseObject_OnTryHeal;
        baseObject.OnTryRequestHealth += BaseObject_OnTryRequestHealth;
        baseObject.OnTryRequestMaxHealth += BaseObject_OnTryRequestMaxHealth;
        baseObject.OnTryToggleIFrame += BaseObject_OnTryToggleIFrame;
        baseObject.OnTryBypassIFrame += BaseObject_OnTryBypassIFrame;

        IEnumerable<EntityEffect> effectSource = baseObject is Entity ? (baseObject as Entity).StatusEffects
                                                                      : null;
        runtimeHP = defaultHPAttributes.RuntimeClone(effectSource);

        OnModuleInit?.Invoke();
    }

    protected virtual void BaseObject_OnTryDamage(int amount, ElementType element,
                                                  EventResponse response) {
        if (!IFrameOn) {
            response.received = true;

            int processedAmount = runtimeHP.DoDamage(amount);
            if (processedAmount > 0) {
                baseObject.PropagateDamage(processedAmount);
                StartCoroutine(ISimulateIFrame());

                if (runtimeHP.Health <= 0) {
                    baseObject.Perish();
                    ToggleIFrame(true);
                }
            }
        } else baseObject.PropagateDamage(0);
    }

    protected virtual void BaseObject_OnTryHeal(int amount, EventResponse response) {
        response.received = true;

        int processedAmount = runtimeHP.DoHeal(amount);
        baseObject.PropagateHeal(processedAmount);
    }


    private void BaseObject_OnTryToggleIFrame(bool on, EventResponse response) {
        response.received = true;
        ToggleIFrame(on);
    }

    private void BaseObject_OnTryBypassIFrame(int amount) {
        int processedAmount = runtimeHP.DoDamage(amount);
        if (processedAmount > 0) {
            baseObject.PropagateDamage(processedAmount);
            StartCoroutine(ISimulateIFrame());
        }
    }

    private void BaseObject_OnTryRequestHealth(EventResponse<int> response) {
        response.received = true;
        response.objectReference = Health;
    }

    private void BaseObject_OnTryRequestMaxHealth(EventResponse<int> response) {
        response.received = true;
        response.objectReference = MaxHealth;
    }

    /// <summary>
    /// Make an entity invulnerable for an unlimited time, until turned back off;
    /// </summary>
    /// <param name="on"> True makes the object invulnerable, false makes it vulnerable; </param>
    public void ToggleIFrame(bool on) {
        externalIFrameOn = on;
    }

    protected virtual IEnumerator ISimulateIFrame() {
        localIFrameOn = true;
        baseObject.ApplyMaterial(iFrameProperties.settings.flashMaterial);
        yield return new WaitForSeconds(iFrameProperties.duration);
        baseObject.ResetMaterials();
        localIFrameOn = false;
    }

    #if UNITY_EDITOR
    protected override void Reset() {
        base.Reset();
        if (CJUtils.AssetUtils.TryRetrieveAsset(out DefaultHealthAttributeCurves curves)) {
            defaultHPAttributes = new(curves);
        }
        if (CJUtils.AssetUtils.TryRetrieveAsset(out DefaultIFrameProperties properties)) {
            iFrameProperties = properties.DefaultProperties;
        }
    }
    #endif
}