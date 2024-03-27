# Fire Protection

```json
{
  "Rules": 
  [
        {
      "Rule Name": "Comments a b c",
      "When Run": "",
      "Categories": [
        "Walls"
      ],
      "Parameter Name": "Comments",
      "List Options":
      [
        {"name": "a", "description": ""},
        {"name": "b", "description": ""},
        {"name": "c", "description": ""},
      ]
    },
    {
      "Rule Name": "Fire Protection Wall Instance Options",
      "When Run": "",
      "Categories": [
        "Walls"
      ],
      "Parameter Name": "ASSEMBLY FIRE PROTECTION",
      "List Options":
      [
        {"name": "EX", "description": ""},
        {"name": "FB", "description": ""},
        {"name": "FP", "description": ""},
        {"name": "FW", "description": ""},
        {"name": "SB", "description": ""},
        {"name": "SP", "description": ""}
      ]
    },
    {
      "Rule Name": "Assembly Height Options",
      "When Run": "",
      "Categories": [
        "Walls"
      ],
      "Parameter Name": "ASSEMBLY HEIGHT CONDITION (ASSIGNED)",
      "List Options":
      [
        {"name": "S", "description": ""},
        {"name": "A", "description": ""}
      ]
    },
    {
      "Rule Name": "Fire Rating Less Than Wall Type Potential",
      "When Run": "",
      "Categories": [
        "Walls"
      ],
      "Parameter Name": "ASSEMBLY FIRE RATING (ASSIGNED) HOURS",
      "Requirement": "<= {ASSEMBLY FIRE RATING (POTENTIAL) HOURS}",
      "User Message": "Assigned hours must be less than or equal to potential hours"
    },
    {
      "Rule Name": "If wall is fire rated, AHC must be S",
      "When Run": "",
      "Categories": [
        "Walls"
      ],
      "Parameter Name": "ASSEMBLY HEIGHT CONDITION (ASSIGNED)",
      "Requirement": "IF {ASSEMBLY FIRE RATING (ASSIGNED) HOURS} >= 0 THEN {ASSEMBLY HEIGHT CONDITION (ASSIGNED)} = S",
      "User Message": "If wall is fire rated, AHC must be S"
    }
  ]
}
```
