#if IL2CPPMELON
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using S1Avatar = Il2CppScheduleOne.Core.Avatar;
#else
using S1Avatar = ScheduleOne.Core.Avatar;
#endif

using HarmonyLib;
using UnityEngine;

namespace DrugExpansion.Content.Mdma.Precursors;

internal static class DiscoDaveyAvatarObjects
{
    internal static void OwnAttachments(S1Avatar.AvatarObject avatarObject)
    {
#if IL2CPPMELON
        var sources = avatarObject._attachments;
#else
        var sources = (S1Avatar.AvatarAttachment[])AccessTools.Field(
            typeof(S1Avatar.AvatarObject), "_attachments").GetValue(avatarObject);
#endif
        var owned = new List<S1Avatar.AvatarAttachment>(sources.Length);
        for (int index = 0; index < sources.Length; index++)
        {
            var source = sources[index];
            if (source == null)
                continue;
            if (source.transform.IsChildOf(avatarObject.transform))
            {
                owned.Add(source);
                continue;
            }

            owned.Add(UnityEngine.Object.Instantiate(source, avatarObject.transform, false));
        }
        // Native AvatarObject.Initialize dereferences every entry, so omit invalid references.
        SetAttachments(avatarObject, owned.ToArray());
    }

    internal static Transform ReplaceWithRigidAttachment(
        S1Avatar.AvatarObject avatarObject,
        float hipHeight)
    {
        OwnAttachments(avatarObject);
        foreach (var attachment in avatarObject.GetComponentsInChildren<S1Avatar.AvatarAttachment>(true))
            attachment.gameObject.SetActive(false);

        var root = new GameObject("DiscoDavey_FestivalSlingBagAttachment");
        root.transform.SetParent(avatarObject.transform, false);
        var attachmentComponent = root.AddComponent<S1Avatar.AvatarAttachment>();
        var alignment = new GameObject("Alignment Point").transform;
        alignment.SetParent(root.transform, false);
        alignment.localPosition = Vector3.up * hipHeight;
#if IL2CPPMELON
        attachmentComponent._anchor = S1Avatar.EAttachmentAnchor.Hips;
        attachmentComponent._alignmentPoint = alignment;
#else
        AccessTools.Field(typeof(S1Avatar.AvatarAttachment), "_anchor")
            .SetValue(attachmentComponent, S1Avatar.EAttachmentAnchor.Hips);
        AccessTools.Field(typeof(S1Avatar.AvatarAttachment), "_alignmentPoint")
            .SetValue(attachmentComponent, alignment);
#endif
        SetAttachments(avatarObject, new[] { attachmentComponent });
        return root.transform;
    }

    private static void SetAttachments(
        S1Avatar.AvatarObject avatarObject,
        S1Avatar.AvatarAttachment[] attachments)
    {
#if IL2CPPMELON
        avatarObject._attachments = new Il2CppReferenceArray<S1Avatar.AvatarAttachment>(attachments);
#else
        AccessTools.Field(typeof(S1Avatar.AvatarObject), "_attachments")
            .SetValue(avatarObject, attachments);
#endif
    }
}
