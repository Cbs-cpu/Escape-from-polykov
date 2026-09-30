namespace Polykov.Weapons
{
    /// <summary>Modding slots of a weapon. Values are stable: they index <see cref="WeaponBuild"/> and its string form.</summary>
    public enum AttachmentSlot : byte
    {
        Muzzle = 0,
        Barrel = 1,
        Grips = 2,
        Magazine = 3,
    }
}
