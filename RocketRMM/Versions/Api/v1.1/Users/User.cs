namespace RocketRMM.Api.v11.Users
{
    public enum UserPhotoSize : uint
    {
        Tiny = 48,
        Small = 120,
        Medium = 240,
        Large = 432,
        Extra_Large = 648
    }

    public class User
    {
        public User()
        {
        }

        public static async Task<string> GetUserPhoto(string userId, UserPhotoSize userPhotoSize, string tenantFilter)
        {
            return await Utilities.Base64Encode(await GraphRequestHelper.NewGraphGetRequestBytes(string.Format("https://graph.microsoft.com/v1.0/users/{0}/photos/{1}x{1}/$value", userId, ((uint)userPhotoSize).ToString()), tenantFilter, contentHeader: "image/jpg"));
        }

    }

}
