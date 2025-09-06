using JetBrains.Annotations;

namespace PeServices.Aps.Models;

public class HubsApi {

public class Hubs {
    [UsedImplicitly] public object Links { get; }
    [UsedImplicitly] public HubsJsonApi JsonApi { get; }
    [UsedImplicitly] public List<HubsHubData> Data { get; }

    public class HubsJsonApi {
        [UsedImplicitly] public string Version { get; }
    }

    public class HubsHubData {
        [UsedImplicitly] public string Type { get; }
        [UsedImplicitly] public string Id { get; } // the important one
        [UsedImplicitly] public HubsHubDataAttributes Attributes { get; }

        public class HubsHubDataAttributes {
            [UsedImplicitly] public string Name { get; }
            [UsedImplicitly] public HubsHubDataAttributesExtension Extension { get; }
            [UsedImplicitly] public string Region { get; }

            public class HubsHubDataAttributesExtension {
                [UsedImplicitly] public string Type { get; }
                [UsedImplicitly] public string Version { get; }
                [UsedImplicitly] public HubsHubDataAttributesExtensionSchema Schema { get; }
                [UsedImplicitly] public object Data { get; }
            }

            public class HubsHubDataAttributesExtensionSchema {
                [UsedImplicitly] public string Href { get; }
            }
        }
    }
}
}

///////////////////////////////////////////////////////////////////////////
// Original JSON response, only certain parts of 'data' are necessary
//////////////////////////////////////////////////////////////////////////
/*
```json
{
  "jsonapi": {
    "version": "1.0"
  },
  "links": {
    "self": {
      "href": "https://developer.api.autodesk.com/project/v1/hubs"
    }
  },
  "data": [
    {
      "type": "hubs",
      "id": "b.6cd2c1f1-f48d-41f4-aa5f-4b51e535595f",
      "attributes": {
        "name": "Positive Energy",
        "extension": {
          "type": "hubs:autodesk.bim360:Account",
          "version": "1.0",
          "schema": {
            "href": "https://developer.api.autodesk.com/schema/v1/versions/hubs:autodesk.bim360:Account-1.0"
          },
          "data": {}
        },
        "region": "US"
      },
      "links": {
        "self": {
          "href": "https://developer.api.autodesk.com/project/v1/hubs/b.6cd2c1f1-f48d-41f4-aa5f-4b51e535595f"
        }
      },
      "relationships": {
        "projects": {
          "links": {
            "related": {
              "href": "https://developer.api.autodesk.com/project/v1/hubs/b.6cd2c1f1-f48d-41f4-aa5f-4b51e535595f/projects"
            }
          }
        }
      }
    }
  ],
  "meta": {
    "warnings": [
      {
        "Id": null,
        "HttpStatusCode": "403",
        "ErrorCode": "BIM360DM_ERROR",
        "Title": "Unable to get hubs from BIM360DM EMEA.",
        "Detail": "You don't have permission to access this API",
        "AboutLink": null,
        "Source": null,
        "meta": null
      },
      ...
      {
        "Id": null,
        "HttpStatusCode": "403",
        "ErrorCode": "BIM360DM_ERROR",
        "Title": "Unable to get hubs from BIM360DM JPN.",
        "Detail": "You don't have permission to access this API",
        "AboutLink": null,
        "Source": null,
        "meta": null
      }
    ]
  }
}
```
*/