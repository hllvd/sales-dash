
https://7a8110990e16404daec259c355434bc6.pbidedicated.windows.net/webapi/capacities/7A811099-0E16-404D-AEC2-59C355434BC6/workloads/QES/QueryExecutionService/automatic/public/query

Payload

track
query
query
subscribe
track
5 / 15 requests
17.8 kB / 18.8 kB transferred
62.0 kB / 95.1 kB resources
{"version":"1.0.0","queries":[{"Query":{"Commands":[{"SemanticQueryDataShapeCommand":{"Query":{"Version":2,"From":[{"Name":"m","Entity":"1_Medidas","Type":0},{"Name":"t","Entity":"tbl_cotas","Type":0},{"Name":"r","Entity":"Regiões","Type":0},{"Name":"p","Entity":"Parâmetro_Senhas","Type":0},{"Name":"a","Entity":"acessos","Type":0}],"Select":[{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Produção Oficial"},"Name":"Medidas.Produção Oficial","NativeReferenceName":"Produção Oficial"},{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Retenção"},"Name":"Medidas.Retenção","NativeReferenceName":"Retenção"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"id_consultor"},"Name":"tbl_cotas.id_consultor","NativeReferenceName":"CNPJ"},{"Column":{"Expression":{"SourceRef":{"Source":"r"}},"Property":"regiao"},"Name":"Regiões.regiao","NativeReferenceName":"Região"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"Consultor_Matricula"},"Name":"tbl_cotas.Consultor_Matricula","NativeReferenceName":"Consultor"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"},"Name":"tbl_cotas.nm_unidade_bi_original","NativeReferenceName":"Unidade Original"}],"Where":[{"Condition":{"Comparison":{"ComparisonKind":1,"Left":{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Produção Oficial"}},"Right":{"Literal":{"Value":"0L"}}}},"Target":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"id_consultor"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"Consultor_Matricula"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"}},{"Column":{"Expression":{"SourceRef":{"Source":"r"}},"Property":"regiao"}}]},{"Condition":{"Comparison":{"ComparisonKind":1,"Left":{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Filtro de Cotas"}},"Right":{"Literal":{"Value":"0L"}}}},"Target":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"id_consultor"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"Consultor_Matricula"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"}},{"Column":{"Expression":{"SourceRef":{"Source":"r"}},"Property":"regiao"}}]},{"Condition":{"In":{"Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"Grupo Ativo"}}],"Values":[[{"Literal":{"Value":"'Sim'"}}]]}}},{"Condition":{"In":{"Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"}}],"Values":[[{"Literal":{"Value":"'BALNEARIO CAMBORIU - SC'"}}]]}}},{"Condition":{"And":{"Left":{"Comparison":{"ComparisonKind":2,"Left":{"Column":{"Expression":{"SourceRef":{"Source":"p"}},"Property":"Parâmetro_Senhas"}},"Right":{"Literal":{"Value":"929009D"}}}},"Right":{"Comparison":{"ComparisonKind":4,"Left":{"Column":{"Expression":{"SourceRef":{"Source":"p"}},"Property":"Parâmetro_Senhas"}},"Right":{"Literal":{"Value":"929009D"}}}}}}},{"Condition":{"In":{"Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"a"}},"Property":"matricula"}}],"Values":[[{"Literal":{"Value":"'011177'"}}]]}}}],"OrderBy":[{"Direction":2,"Expression":{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Produção Oficial"}}}]},"Binding":{"Primary":{"Groupings":[{"Projections":[0,1,2,3,4,5],"Subtotal":1}]},"DataReduction":{"DataVolume":3,"Primary":{"Window":{"Count":500}}},"Version":1},"ExecutionMetricsKind":1}}]},"QueryId":"","ApplicationContext":{"DatasetId":"ecfe45f3-4e9f-4a6e-9d56-91020972365d","Sources":[{"ReportId":"0fdd545d-8c7f-4a8a-9723-daf16cac10d6","VisualId":"081f7f5c60a98980243b"}]}}],"cancelQueries":[],"modelId":5258155,"userPreferredLocale":"en-US","allowLongRunningQueries":true}

RESPONSE:
{
    "results": [
        {
            "jobId": "0",
            "fromCache": false,
            "result": {
                "data": {
                    "timestamp": "/Date(1776030688554)/",
                    "rootActivityId": "76cfb3c6-7478-4b4c-8519-08ec7581c682",
                    "descriptor": {
                        "Select": [
                            {
                                "Kind": 2,
                                "Value": "M0",
                                "Subtotal": [
                                    "A0"
                                ],
                                "Name": "Medidas.Produção Oficial"
                            },
                            {
                                "Kind": 2,
                                "Value": "M1",
                                "Format": "0%;-0%;0%",
                                "Subtotal": [
                                    "A1"
                                ],
                                "Name": "Medidas.Retenção"
                            },
                            {
                                "Kind": 1,
                                "Depth": 0,
                                "Value": "G0",
                                "GroupKeys": [
                                    {
                                        "Source": {
                                            "Entity": "tbl_cotas",
                                            "Property": "id_consultor"
                                        },
                                        "Calc": "G0",
                                        "IsSameAsSelect": true
                                    }
                                ],
                                "Name": "tbl_cotas.id_consultor"
                            },
                            {
                                "Kind": 1,
                                "Depth": 0,
                                "Value": "G1",
                                "GroupKeys": [
                                    {
                                        "Source": {
                                            "Entity": "Regiões",
                                            "Property": "regiao"
                                        },
                                        "Calc": "G1",
                                        "IsSameAsSelect": true
                                    }
                                ],
                                "Name": "Regiões.regiao"
                            },
                            {
                                "Kind": 1,
                                "Depth": 0,
                                "Value": "G2",
                                "GroupKeys": [
                                    {
                                        "Source": {
                                            "Entity": "tbl_cotas",
                                            "Property": "Consultor_Matricula"
                                        },
                                        "Calc": "G2",
                                        "IsSameAsSelect": true
                                    }
                                ],
                                "Name": "tbl_cotas.Consultor_Matricula"
                            },
                            {
                                "Kind": 1,
                                "Depth": 0,
                                "Value": "G3",
                                "GroupKeys": [
                                    {
                                        "Source": {
                                            "Entity": "tbl_cotas",
                                            "Property": "nm_unidade_bi_original"
                                        },
                                        "Calc": "G3",
                                        "IsSameAsSelect": true
                                    }
                                ],
                                "Name": "tbl_cotas.nm_unidade_bi_original"
                            }
                        ],
                        "Expressions": {
                            "Primary": {
                                "Groupings": [
                                    {
                                        "Keys": [
                                            {
                                                "Source": {
                                                    "Entity": "tbl_cotas",
                                                    "Property": "id_consultor"
                                                },
                                                "Select": 2
                                            },
                                            {
                                                "Source": {
                                                    "Entity": "Regiões",
                                                    "Property": "regiao"
                                                },
                                                "Select": 3
                                            },
                                            {
                                                "Source": {
                                                    "Entity": "tbl_cotas",
                                                    "Property": "Consultor_Matricula"
                                                },
                                                "Select": 4
                                            },
                                            {
                                                "Source": {
                                                    "Entity": "tbl_cotas",
                                                    "Property": "nm_unidade_bi_original"
                                                },
                                                "Select": 5
                                            }
                                        ],
                                        "Member": "DM1",
                                        "SubtotalMember": "DM0"
                                    }
                                ]
                            }
                        },
                        "Version": 2
                    },
                    "dsr": {
                        "Version": 2,
                        "MinorVersion": 1,
                        "DS": [
                            {
                                "N": "DS0",
                                "PH": [
                                    {
                                        "DM0": [
                                            {
                                                "S": [
                                                    {
                                                        "N": "A0",
                                                        "T": 3
                                                    },
                                                    {
                                                        "N": "A1",
                                                        "T": 3
                                                    }
                                                ],
                                                "C": [
                                                    374205035.41,
                                                    "0.8777711674032227"
                                                ]
                                            }
                                        ]
                                    },
                                    {
                                        "DM1": [
                                            {
                                                "S": [
                                                    {
                                                        "N": "G0",
                                                        "T": 1,
                                                        "DN": "D0"
                                                    },
                                                    {
                                                        "N": "G1",
                                                        "T": 1,
                                                        "DN": "D1"
                                                    },
                                                    {
                                                        "N": "G2",
                                                        "T": 1,
                                                        "DN": "D2"
                                                    },
                                                    {
                                                        "N": "G3",
                                                        "T": 1,
                                                        "DN": "D3"
                                                    },
                                                    {
                                                        "N": "M0",
                                                        "T": 3
                                                    },
                                                    {
                                                        "N": "M1",
                                                        "T": 3
                                                    }
                                                ],
                                                "C": [
                                                    0,
                                                    0,
                                                    0,
                                                    0,
                                                    184206534.23,
                                                    "0.79257095417532086"
                                                ]
                                            },
                                            {
                                                "C": [
                                                    1,
                                                    1,
                                                    85869288,
                                                    "0.95446567578387276"
                                                ],
                                                "R": 10
                                            },
                                            {
                                                "C": [
                                                    2,
                                                    2,
                                                    36891312,
                                                    "0.997289334681293"
                                                ],
                                                "R": 10
                                            },
                                            {
                                                "C": [
                                                    3,
                                                    3,
                                                    21809336,
                                                    1
                                                ],
                                                "R": 10
                                            },
                                            {
                                                "C": [
                                                    4,
                                                    4,
                                                    10344661,
                                                    "0.88379425870021255"
                                                ],
                                                "R": 10
                                            },
                                            {
                                                "C": [
                                                    5,
                                                    5,
                                                    10295000,
                                                    "0.93200582807187959"
                                                ],
                                                "R": 10
                                            },
                                            {
                                                "C": [
                                                    6,
                                                    6,
                                                    5440000,
                                                    "0.94485294117647056"
                                                ],
                                                "R": 10
                                            },
                                            {
                                                "C": [
                                                    7,
                                                    7,
                                                    5406490,
                                                    "0.852630819626042"
                                                ],
                                                "R": 10
                                            },
                                            {
                                                "C": [
                                                    8,
                                                    8,
                                                    4811660,
                                                    "0.89192918867916682"
                                                ],
                                                "R": 10
                                            },
                                            {
                                                "C": [
                                                    9,
                                                    9,
                                                    3795000,
                                                    1
                                                ],
                                                "R": 10
                                            },
                                            {
                                                "C": [
                                                    10,
                                                    10,
                                                    "2550754.1799999997"
                                                ],
                                                "R": 42
                                            },
                                            {
                                                "C": [
                                                    11,
                                                    11,
                                                    2130000
                                                ],
                                                "R": 42
                                            },
                                            {
                                                "C": [
                                                    12,
                                                    12,
                                                    430000
                                                ],
                                                "R": 42
                                            },
                                            {
                                                "C": [
                                                    13,
                                                    13,
                                                    225000
                                                ],
                                                "R": 42
                                            }
                                        ]
                                    }
                                ],
                                "IC": true,
                                "HAD": true,
                                "ValueDicts": {
                                    "D0": [
                                        "58020087000149",
                                        "57845748000102",
                                        "55195504000104",
                                        "57442936000190",
                                        "61943900000167",
                                        "50453150000129",
                                        "61766584000103",
                                        "31205822000132",
                                        "61328520000112",
                                        "64039490000112",
                                        "45004193000197",
                                        "63441781000170",
                                        "63161393000135",
                                        "62403007000101"
                                    ],
                                    "D1": [
                                        "Sul"
                                    ],
                                    "D2": [
                                        "011177 KNAAN INVESTIMENTOS LTDA",
                                        "010134 RHIZA INVESTIMENTOS LTDA",
                                        "008612 HAZAB INVESTIMENTOS LTDA",
                                        "010135 DANILOW \u0026 LIMA LTDA",
                                        "011940 AGAPE INVESTIMENTOS LTDA",
                                        "007008 LH SERVI\u00c7OS FINANCEIROS LTDA",
                                        "012718 ZINELLI INVESTIMENTOS LTDA",
                                        "003635 BLESSING CORRET. DE CONS. LTDA",
                                        "011628 E\u0026R CONSULTORIA E INVESTIMENTOS LTDA",
                                        "013614 LB CONSULTORIA E INVESTIMENTOS LTDA",
                                        "006760 QUALITY HOME INVEST LTDA",
                                        "013641 NERIAH INVESTIMENTOS LTDA",
                                        "013050 ZION INVESTIMENTOS LTDA",
                                        "013163 MANAH INVESTIMENTOS LTDA"
                                    ],
                                    "D3": [
                                        "BALNEARIO CAMBORIU - SC"
                                    ]
                                }
                            }
                        ]
                    },
                    "metrics": {
                        "Version": "1.0.0",
                        "Events": [
                            {
                                "Id": "886d3486-0cf7-43f5-bf3a-4cdaf47f0304",
                                "Name": "Execute Semantic Query",
                                "Component": "DSE",
                                "Start": "2026-04-12T21:51:28.554737Z",
                                "End": "2026-04-12T21:51:29.7434115Z"
                            },
                            {
                                "Id": "b172243c-f141-4218-b712-b0cd04282bd3",
                                "ParentId": "886d3486-0cf7-43f5-bf3a-4cdaf47f0304",
                                "Name": "Execute DAX Query",
                                "Component": "DSE",
                                "Start": "2026-04-12T21:51:28.5703761Z",
                                "End": "2026-04-12T21:51:29.7434115Z",
                                "Metrics": {
                                    "RowCount": 15
                                }
                            },
                            {
                                "Id": "29612EA4-0FE9-4937-B71F-1C0DF0C1135D",
                                "ParentId": "b172243c-f141-4218-b712-b0cd04282bd3",
                                "Name": "Execute Query",
                                "Component": "AS",
                                "Start": "2026-04-12T21:51:28.597Z",
                                "End": "2026-04-12T21:51:29.703Z"
                            },
                            {
                                "Id": "BDDA866E-F1DD-43CC-8896-D0378A8659CE",
                                "ParentId": "29612EA4-0FE9-4937-B71F-1C0DF0C1135D",
                                "Name": "Serialize Rowset",
                                "Component": "AS",
                                "Start": "2026-04-12T21:51:29.703Z",
                                "End": "2026-04-12T21:51:29.703Z"
                            }
                        ]
                    }
                }
            }
        }
    ],
    "jobIds": [
        "0"
    ]
}