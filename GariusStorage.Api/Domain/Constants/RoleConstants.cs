namespace GariusStorage.Api.Domain.Constants
{
    public static class RoleConstants
    {
        public static readonly Guid AdminRoleId = new Guid("b7da6419-3cb9-4bb0-8684-f79e4500da75");
        public static readonly Guid OwnerRoleId = new Guid("a22a6ce0-b66d-4dca-9482-bd9276f7738e");
        public static readonly Guid DeveloperRoleId = new Guid("3fcc0a7b-abe8-4a67-b757-69b4797edb20");
        public static readonly Guid UserRoleId = new Guid("f62a600f-64ba-40b1-8e7d-89ebca0b9832");

        public const string AdminRoleName = "Admin";
        public const string OwnerRoleName = "Owner";
        public const string DeveloperRoleName = "Developer";
        public const string UserRoleName = "User";
    }
}
