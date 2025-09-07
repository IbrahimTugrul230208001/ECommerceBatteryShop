namespace ECommerceBatteryShop.Services
{
    public class UserService:IUserService
    {

            private string? _verificationCode;
            private string? _email;
            private string? _password;
            private int _userId;

            public string VerificationCode
            {
                get => _verificationCode;
                set => _verificationCode = value;
            }
            public string Email
            {
                get => _email;
                set => _email = value;
            }
            public string Password
            {
                get => _password;
                set => _password = value;
            }
            public int UserId
            {
                get => _userId;
                set => _userId = value;
            }
        }


    }

