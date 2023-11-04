using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RocketRMM
{
    internal class EntraSam
    {
        public enum SamAppType { Api, Spa };

        internal static async Task<SamAndPassword> CreateSAMAuthApp(string appName, SamAppType appType, string domain, string swaggerUiAppId = "", string[]? spaRedirectUri = null, string scopeGuid = "")
        {
            dynamic samApp;

            switch (appType)
            {
                case SamAppType.Api:
                    samApp = new ExpandoObject();
                    samApp.displayName = appName;
                    samApp.requiredResourceAccess = new List<RequiredResourceAccess>()
                    {
                        new()
                        {
                            ResourceAccess = [
                                new()
                                {
                                    Id = new Guid("128ca929-1a19-45e6-a3b8-435ec44a36ba"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("e1fe6dd8-ba31-4d61-89e7-88639da4683d"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("aa07f155-3612-49b8-a147-6c590df35536"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("0f4595f7-64b1-4e13-81bc-11a249df07a9"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("73e75199-7c3e-41bb-9357-167164dbb415"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("7ab1d787-bae7-4d5d-8db6-37ea32df9186"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("d01b97e9-cbc0-49fe-810a-750afd5527a3"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("46ca0847-7e6b-426e-9775-ea810a948356"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("dc38509c-b87d-4da0-bd92-6bec988bac4a"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("7427e0e9-2fba-42fe-b0c0-848c9e6a8182"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("ad902697-1014-4ef5-81ef-2b4301988e8c"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("572fea84-0151-49b2-9301-11cb16974376"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("e4c9e354-4dc5-45b8-9e7c-e1393b0b1a20"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("0883f392-0a7a-443d-8c76-16a6d39c7b63"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("7b3f05d5-f68c-4b8d-8c59-a2ecd12f24af"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("0c5e8a55-87a6-4556-93ab-adc52c4d862d"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("44642bfe-8385-4adc-8fc6-fe3cb2c375c3"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("662ed50a-ac44-4eef-ad86-62eed9be2a29"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("8696daa5-bce5-4b2e-83f9-51b6defc4e1e"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("6aedf524-7e1c-45a7-bd76-ded8cab8d0fc"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("bac3b9c2-b516-4ef4-bd3b-c2ef73d8d804"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("11d4cd79-5ba5-460f-803f-e22c8ab85ccd"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("02e97553-ed7b-43d0-ab3c-f8bace0d040c"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("89fe6a52-be36-487e-b7d8-d061c450a026"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("a367ab51-6b49-43bf-a716-a1fb06d2a174"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("204e0828-b5ca-4ad8-b9f3-f32a958e7cc4"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("4e46008b-f24c-477d-8fff-7bb4ec7aafe0"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("0e263e50-5827-48a4-b97c-d940288653c7"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("e383f46e-2787-4529-855e-0e479a3ffac0"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("37f7f235-527c-4136-accd-4a02d197296e"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("14dad69e-099b-42c9-810b-d002981feec1"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("f6a3db3e-f7e8-4ed2-a414-557c8c9830be"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("0e755559-83fb-4b44-91d0-4cc721b9323e"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("a84a9652-ffd3-496e-a991-22ba5529156a"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("1d89d70c-dcac-4248-b214-903c457af83a"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("2b61aa8a-6d36-4b2f-ac7b-f29867937c53"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("ebf0f66e-9fb1-49e4-a278-222f76911cf4"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("c79f8feb-a9db-4090-85f9-90d820caa0eb"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("bdfbf15f-ee85-4955-8675-146e8e5296b5"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("f81125ac-d3b7-4573-a3b2-7099cc39df9e"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("cac97e40-6730-457d-ad8d-4852fddab7ad"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("b7887744-6746-4312-813d-72daeaee7e2d"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("48971fc1-70d7-4245-af77-0beb29b53ee2"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("aec28ec7-4d02-4e8c-b864-50163aea77eb"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("a9ff19c2-f369-4a95-9a25-ba9d460efc8e"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("59dacb05-e88d-4c13-a684-59f1afc8cc98"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("b98bfd41-87c6-45cc-b104-e2de4f0dafb9"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("2f9ee017-59c1-4f1d-9472-bd5529a7b311"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("951183d1-1a61-466f-a6d1-1fde911bfd95"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("637d7bec-b31e-4deb-acc9-24275642a2c9"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("101147cf-4178-4455-9d58-02b5c164e759"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("cc83893a-e232-4723-b5af-bd0b01bcfe65"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("233e0cf1-dd62-48bc-b65b-b38fe87fcf8e"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("d649fb7c-72b4-4eec-b2b4-b15acf79e378"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("485be79e-c497-4b35-9400-0e3fa7f2a5d4"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("9d8982ae-4365-4f57-95e9-d6032a4c0b87"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("48638b3c-ad68-4383-8ac4-e6880ee6ca57"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("39d65650-9d3e-4223-80db-a335590d027e"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("4a06efd2-f825-4e34-813e-82a57b03d1ee"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("f3bfad56-966e-4590-a536-82ecf548ac1e"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("4d135e65-66b8-41a8-9f8b-081452c91774"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("2eadaff8-0bce-4198-a6b9-2cfc35a30075"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("0c3e411a-ce45-4cd1-8f30-f99a3efa7b11"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("edb72de9-4252-4d03-a925-451deef99db7"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("767156cb-16ae-4d10-8f8b-41b657c8c8c8"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("7e823077-d88e-468f-a337-e18f1f0e6c7c"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("edd3c878-b384-41fd-95ad-e7407dd775be"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("40b534c3-9552-4550-901b-23879c90bcf9"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("bf3fbf03-f35f-4e93-963e-47e4d874c37a"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("5248dcb1-f83b-4ec3-9f4d-a4428a961a72"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("c395395c-ff9a-4dba-bc1f-8372ba9dca84"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("2e25a044-2580-450d-8859-42eeb6e996c0"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("0ce33576-30e8-43b7-99e5-62f8569a4002"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("207e0cb1-3ce7-4922-b991-5a760c346ebc"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("093f8818-d05f-49b8-95bc-9d2a73e9a43c"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("7825d5d6-6049-4ce7-bdf6-3b8d53f4bcd0"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("2104a4db-3a2f-4ea0-9dba-143d457dc666"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("eda39fa6-f8cf-4c3c-a909-432c683e4c9b"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("55896846-df78-47a7-aa94-8d3d4442ca7f"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("aa85bf13-d771-4d5d-a9e6-bca04ce44edf"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("ee928332-e9c2-4747-b4a0-f8c164b68de6"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("c975dd04-a06e-4fbb-9704-62daad77bb49"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("c37c9b61-7762-4bff-a156-afc0005847a0"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("34bf0e97-1971-4929-b999-9e2442d941d7"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("19b94e34-907c-4f43-bde9-38b1909ed408"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("999f8c63-0a38-4f1b-91fd-ed1947bdd1a9"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("292d869f-3427-49a8-9dab-8c70152b74e9"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("2f51be20-0bb4-4fed-bf7b-db946066c75e"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("58ca0d9a-1575-47e1-a3cb-007ef2e4583b"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("06a5fe6d-c49d-46a7-b082-56b1b14103c7"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("246dd0d5-5bd0-4def-940b-0421030a5b68"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("bf394140-e372-4bf9-a898-299cfc7564e5"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("741f803b-c850-494e-b5df-cde7c675a1ca"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("230c1aed-a721-4c5d-9cb4-a90514e508ef"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("b633e1c5-b582-4048-a93e-9f11b44c7e96"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("5b567255-7703-4780-807c-7be8301ae99b"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("62a82d76-70ea-41e2-9197-370581804d09"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("7ab1d382-f21e-4acd-a863-ba3e13f7da61"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("1138cb37-bd11-4084-a2b7-9f71582aeddb"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("78145de6-330d-4800-a6ce-494ff2d33d07"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("9241abd9-d0e6-425a-bd4f-47ba86e767a4"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("5b07b0dd-2377-4e44-a38d-703f09a0dc3c"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("243333ab-4d21-40cb-a475-36241daa0842"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("e330c4f0-4170-414e-a55a-2f022ec2b57b"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("5ac13192-7ace-4fcf-b828-1a26f28068ee"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("2f6817f8-7b12-4f0f-bc18-eeaf60705a9e"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("dbaae8cf-10b5-4b86-a4a1-f871c94c6695"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("bf7b1a76-6e77-406b-b258-bf5c7720e98f"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("01c0a623-fc9b-48e9-b794-0756f8e8f067"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("50483e42-d915-4231-9639-7fdb7fd190e5"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("dbb9058a-0e50-45d7-ae91-66909b5d4664"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("a82116e5-55eb-4c41-a434-62fe8a61c773"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("f3a65bd4-b703-46df-8f7e-0174fea562aa"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("59a6b24b-4225-4393-8165-ebaec5f55d7a"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("0121dc95-1b9f-4aed-8bac-58c5ac466691"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("3b55498e-47ec-484f-8136-9013221c06a9"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("35930dcf-aceb-4bd1-b99a-8ffed403c974"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("25f85f3c-f66c-4205-8cd5-de92dd7f0cec"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("29c18626-4985-4dcd-85c0-193eef327366"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("4437522e-9a86-4a41-a7da-e380edd4a97d"),
                                    Type = "Role"
                                }
                            ],
                            ResourceAppId = new Guid("0000000300000000c000000000000000")
                        },
                        new()
                        {
                            ResourceAccess = [
                                new()
                                {
                                    Id = new Guid("1cebfa2a-fb4d-419e-b5f9-839b4383e05a"),
                                    Type = "Scope"
                                }
                            ],
                            ResourceAppId = new Guid("fa3d9a0c-3fb0-42cc-9193-47c7ecd2edbd")
                        },
                        new()
                        {
                            ResourceAccess = [
                                new()
                                {
                                    Id = new Guid("5778995a-e1bf-45b8-affa-663a9f3f4d04"),
                                    Type = "Role"
                                },
                                new()
                                {
                                    Id = new Guid("a42657d6-7f20-40e3-b6f0-cee03008a62a"),
                                    Type = "Scope"
                                },
                                new()
                                {
                                    Id = new Guid("311a71cc-e848-46a1-bdf8-97ff7156d8e6"),
                                    Type = "Scope"
                                },
                            ],
                            ResourceAppId = new Guid("00000002-0000-0000-c000-000000000000")
                        }

                    };

                    if (!swaggerUiAppId.Equals(string.Empty) && !scopeGuid.Equals(string.Empty))
                    {
                        samApp.identifierUris = new List<string>() { string.Format("https://{0}/{1}", domain, Guid.NewGuid().ToString()) };

                        samApp.api = new ApiApplication()
                        {
                            AcceptMappedClaims = null,
                            KnownClientApplications = [],
                            RequestedAccessTokenVersion = 2,
                            Oauth2PermissionScopes = [
                                new PermissionScope
                                {
                                    Id = scopeGuid,
                                    AdminConsentDescription = "access the api",
                                    AdminConsentDisplayName = "access the api",
                                    IsEnabled = true,
                                    Type = "Admin",
                                    UserConsentDescription = "access the api",
                                    UserConsentDisplayName = "access the api",
                                    Value = CoreEnvironment.ApiAccessScope
                                }
                            ],
                            PreAuthorizedApplications = [
                                new PreAuthorizedApplication
                                {
                                    AppId = swaggerUiAppId,
                                    DelegatedPermissionIds = [scopeGuid.ToString()]
                                }
                            ]
                        };
                    }
                    else
                    {
                        samApp.identifierUris = new List<string>() { string.Format("https://{0}/{1}", domain, Guid.NewGuid().ToString()) };

                        samApp.api = new ApiApplication()
                        {
                            AcceptMappedClaims = null,
                            KnownClientApplications = [],
                            RequestedAccessTokenVersion = 2,
                            Oauth2PermissionScopes = [
                                new PermissionScope
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    AdminConsentDescription = "access the api",
                                    AdminConsentDisplayName = "access the api",
                                    IsEnabled = true,
                                    Type = "Admin",
                                    UserConsentDescription = "access the api",
                                    UserConsentDisplayName = "access the api",
                                    Value = CoreEnvironment.ApiAccessScope
                                }
                            ],
                            PreAuthorizedApplications = new() { }
                        };
                    }
                    samApp.appRoles = new List<AppRole>()
                    {
                        new()
                        {
                            AllowedMemberTypes = ["User"],
                            Description = "reader",
                            DisplayName = "reader",
                            Id = Guid.NewGuid().ToString(),
                            IsEnabled = true,
                            Origin = "application",
                            Value = "reader"
                        },
                        new()
                        {
                            AllowedMemberTypes = ["User"],
                            Description = "tech",
                            DisplayName = "tech",
                            Id = Guid.NewGuid().ToString(),
                            IsEnabled = true,
                            Origin = "application",
                            Value = "editor"
                        },
                        new()
                        {
                            AllowedMemberTypes = ["User"],
                            Description = "admin",
                            DisplayName = "admin",
                            Id = Guid.NewGuid().ToString(),
                            IsEnabled = true,
                            Origin = "application",
                            Value = "admin"
                        },
                        new()
                        {
                            AllowedMemberTypes = ["User"],
                            Description = "owner",
                            DisplayName = "owner",
                            Id = Guid.NewGuid().ToString(),
                            IsEnabled = true,
                            Origin = "application",
                            Value = "owner"
                        }
                    };
                    samApp.signInAudience = "AzureADMultipleOrgs";
                    samApp.isFallbackPublicClient = true;
                    string rocketRmmFrontEnd = CoreEnvironment.RocketRmmFrontEndUri.TrimEnd('/');
                    samApp.web = new Web()
                    {
                        RedirectUris = [
                            $"{CoreEnvironment.KestrelHttp}",
                            $"{CoreEnvironment.KestrelHttps}",
                            rocketRmmFrontEnd,
                            $"{rocketRmmFrontEnd}/bootstrap/receivegraphtoken",
                            $"{CoreEnvironment.KestrelHttp}/bootstrap/receivegraphtoken",
                            $"{CoreEnvironment.KestrelHttps}/bootstrap/receivegraphtoken",
                            "https://login.microsoftonline.com/common/oauth2/nativeclient",
                            "urn:ietf:wg:oauth:2.0:oob"
                        ],
                        ImplicitGrantSettings = new()
                        {
                            EnableAccessTokenIssuance = true,
                            EnableIdTokenIssuance = true
                        }
                    };

                    JsonElement createdSamApp = await GraphRequestHelper.NewGraphPostRequest("https://graph.microsoft.com/v1.0/applications", CoreEnvironment.Secrets.TenantId, samApp, HttpMethod.Post, "https://graph.microsoft.com/.default", true);
                    Utilities.ConsoleColourWriteLine("Waiting 30 seconds for app to progagate through Azure before setting a password on it...");
                    await Task.Delay(30000); // Have to wait about 30 seconds for Azure to properly replicate the app before we can set password on it
                    var appPasswordJson = await GraphRequestHelper.NewGraphPostRequest($"https://graph.microsoft.com/v1.0/applications/{createdSamApp.GetProperty("id").GetString()}/addPassword", CoreEnvironment.Secrets.TenantId, new PasswordCredential() { DisplayName = "RocketRMM-Pwd", EndDateTime = DateTime.UtcNow.AddYears(5) }, HttpMethod.Post, "https://graph.microsoft.com/.default", true);
                    var servicePrincipleJson = await GraphRequestHelper.NewGraphPostRequest("https://graph.microsoft.com/v1.0/servicePrincipals", CoreEnvironment.Secrets.TenantId, new AppId() { appId = createdSamApp.GetProperty("appId").GetString() }, HttpMethod.Post, "https://graph.microsoft.com/.default", true);
                    var adminAgentGroupJson = await GraphRequestHelper.NewGraphGetRequest("https://graph.microsoft.com/v1.0/groups?$filter=startswith(displayName,'AdminAgents')&$select=id", CoreEnvironment.Secrets.TenantId, "https://graph.microsoft.com/.default", true);
                    string jsonString = $@"{{""@odata.id"":""https://graph.microsoft.com/v1.0/servicePrincipals/{servicePrincipleJson.GetProperty("id").GetString()}""}}";
                    Utilities.ConsoleColourWriteLine("Waiting 30 seconds for Service Principal to progagate through Azure before assigning it as a member of AdminAgents group...");
                    await Task.Delay(30000);
                    await GraphRequestHelper.NewGraphPostRequest($"https://graph.microsoft.com/v1.0/groups/{adminAgentGroupJson[0].GetProperty("id").GetString()}/members/$ref", CoreEnvironment.Secrets.TenantId, JsonSerializer.Deserialize<JsonElement>(jsonString), HttpMethod.Post, "https://graph.microsoft.com/.default", true);
                    return new() { EntraSam = createdSamApp, AppPassword = appPasswordJson.GetProperty("secretText").GetString() ?? string.Empty };

                case SamAppType.Spa:
                    samApp = new ExpandoObject();
                    samApp.displayName = appName;
                    samApp.signInAudience = "AzureADMyOrg";
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    samApp.requiredResourceAccess = new List<RequiredResourceAccess>() { new() { ResourceAccess = [new() { Id = new Guid("e1fe6dd8ba314d6189e788639da4683d"), Type = "Scope" }], ResourceAppId = new Guid("0000000300000000c000000000000000") } };

                    if (spaRedirectUri != null)
                    {
                        samApp.spa = new Spa() { RedirectUris = spaRedirectUri };
                    }

                    return new() { EntraSam = await GraphRequestHelper.NewGraphPostRequest("https://graph.microsoft.com/v1.0/applications", CoreEnvironment.Secrets.TenantId, samApp, HttpMethod.Post, "https://graph.microsoft.com/.default", true) };
            }

            return new();
        }

        internal struct SamAndPassword
        {
            [JsonInclude]
            internal JsonElement EntraSam { get; set; }
            [JsonInclude]
            internal string? AppPassword { get; set; }
        }

        internal struct ResourceAccess
        {
            [JsonInclude]
            internal Guid Id { get; set; }
            [JsonInclude]
            internal string Type { get; set; }
        }

        internal struct RequiredResourceAccess
        {
            [JsonInclude]
            internal List<ResourceAccess> ResourceAccess { get; set; }
            [JsonInclude]
            internal Guid ResourceAppId { get; set; }
        }

        internal struct Spa
        {
            [JsonInclude]
            internal string[]? RedirectUris { get; set; }
        }

        internal struct ApiApplication
        {
            [JsonInclude]
            internal bool? AcceptMappedClaims { get; set; }
            [JsonInclude]
            internal List<string>? KnownClientApplications { get; set; }
            [JsonInclude]
            internal List<PermissionScope>? Oauth2PermissionScopes { get; set; }
            [JsonInclude]
            internal List<PreAuthorizedApplication>? PreAuthorizedApplications { get; set; }
            [JsonInclude]
            internal int? RequestedAccessTokenVersion { get; set; }
        }

        internal struct PermissionScope
        {
            [JsonInclude]
            internal string? Id { get; set; }
            [JsonInclude]
            internal string? AdminConsentDisplayName { get; set; }
            [JsonInclude]
            internal string? AdminConsentDescription { get; set; }
            [JsonInclude]
            internal string? UserConsentDisplayName { get; set; }
            [JsonInclude]
            internal string? UserConsentDescription { get; set; }
            [JsonInclude]
            internal string? Value { get; set; }
            [JsonInclude]
            internal string? Type { get; set; }
            [JsonInclude]
            internal bool? IsEnabled { get; set; }
        }

        internal struct PreAuthorizedApplication
        {
            [JsonInclude]
            internal string? AppId { get; set; }
            [JsonInclude]
            internal List<string>? DelegatedPermissionIds { get; set; }
        }


        internal struct AppRole
        {
            [JsonInclude]
            internal List<string>? AllowedMemberTypes { get; set; }
            [JsonInclude]
            internal string? Description { get; set; }
            [JsonInclude]
            internal string? DisplayName { get; set; }
            [JsonInclude]
            internal string? Id { get; set; }
            [JsonInclude]
            internal bool? IsEnabled { get; set; }
            [JsonInclude]
            internal string? Origin { get; set; }
            [JsonInclude]
            internal string? Value { get; set; }
        }

        internal struct Web
        {
            [JsonInclude]
            internal List<string> RedirectUris { get; set; }
            [JsonInclude]
            internal ImplicitGrantSettings ImplicitGrantSettings { get; set; }
        }

        internal struct ImplicitGrantSettings
        {
            [JsonInclude]
            internal bool EnableAccessTokenIssuance { get; set; }
            [JsonInclude]
            internal bool EnableIdTokenIssuance { get; set; }
        }

        internal struct PasswordCredential
        {
            [JsonInclude]
            internal string DisplayName { get; set; }
            [JsonInclude]
            internal DateTime EndDateTime { get; set; }
        }

        internal struct AppId
        {
            [JsonInclude]
            internal string appId { get; set; }
        }
    }
}
