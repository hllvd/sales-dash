// extract_ademicon.js
// Run with: node extract_ademicon.js

const https = require('https');
const fs    = require('fs');

// ═══════════════════════════════════════════════════════════════
// ✏️  CHANGE THESE THREE VARIABLES BEFORE RUNNING
// ═══════════════════════════════════════════════════════════════

const TOKEN = 'MWCToken eyJhbGciOiJSU0EtT0FFUCIsImVuYyI6IkExMjhDQkMtSFMyNTYiLCJraWQiOiI3NjlGODk2NEQxNzA4Q0VCMDgxQzU4MTBGQ0I2RUJERUZFRjcwRUU4IiwidHlwIjoiSldUIiwiY3R5IjoiSldUIn0.bhc7HfJswOhUuBdk4w75tCllwSYY6zQBZhcb20APfe8o8BQ_GgF7RxuCL1psRmJE8xHxNMkGps0X-ee_4AnmVpwxdPpJ_ADJg5cPVC1nd0X7y1xZQK7IimEs-3qw-nHwqB925Kn1-0LOcYpCRzSXXNgtLK4_WknX4j42wNK2Dgo31zbU7xS_KhfeDtUmw5GSAXOfMGmX18oh62YuwB80MdGRLIRNK8hvu8pIRywG2sroOrASF6vWuz10uIffDTR9-LjyK2t4gkBjPO7kMormEpRjloRx1R3Fm2_hFNo7dBexNpcSO72wKwpijxW8oCpH2e_AirDQ5DtliB8OsRcR6Q.jKKrfRlhVn5fzNCL8Yx6Aw.X7vXRVTiMSh2gQ08HOMX-sZgEIW8KgZ_9PPFIbxxocFne-OVvepuKLpMBKL2BkdsEyJIZ2582V3OZRiy2QeKjYe9jQWj-wxwb6MR_ScrdAKpbZfyrv-8J-D-HjlYT0TSHr1XXEXY2duoU5wvIJkUYeeASpD6h0LWb0Z5cv_41TNXDln1naQ1AdKn_OydVD2NVIgMp0mh7wC7wwcSpDHSOTLrq6OhMJgK8few3KJXawkZ42qZFfuK9RKu0dqX3HSSaw-wyaT1B-SJ5mUaZ0OMnOj2WWVfWO2UCWVcAWds7y6weZ4oROsNoq2eIeJl7jFtpYFdS6Lnx6PW3fpgJ92dij7YpNT_x1-PWXwuwc4r9kCHfC3Bdgg7KCaiVsHgauNwTx8EHCIpaoOOoietSAVc9P1Uc1n0r5cbBglG9OIHh_CdfqbguzVRBNpYEbfmTwh1hfz8XvLVKXqNhIfKL3iS6_a76XYD5xWxJZZbUMUjf3faVyU4hsJ2V5QZ2xwGSmYqdCv7dMxs9BFcpX6E1OmDXVXz3_WHiI_sUVq7DG78pFkXM8PHjYfJ08BxxPuwok8bIb_sHp312ZXNcDeiTlf2j8KJE1vZa4G41ZEhvKhHhQZB2w9lZnid-FVHT5W40EomDN49VajHmwNt9TLqa3HtbzMyPMmJzMPQ31nUfacbzaQbka-ifRYHxpE7zxTPfd72-nUHLV9yK-KrN0ZeZM-w_a1gbV0gE5A7v5z6_JfZGo4MWwgpbDGvcjHPJ6xz95gDnDCHazsh1HqQm18rh5AxfqvGImPwwPe3T-0UKRjt_-gevcgOQTpkjwUNvzu2C4wrLSfomI37c6U7Jh9XFK1i5RwPEyWzXaRDfcYvmYEoplhYEPI3Zr79ByQ7I0O2xuxZ1Iq4nv2bOnOgrZA7cfGHTVuj8VSAgahW2C4GSQdA1LTu5s8vkVw5DvQN309EnTZxqC-hURmveKJ-hWK3SNhTBoZtcen_2ONtExUCYhk7tSNLAa6SJRy-KyP4Up62MC6zwTZ7SthKaK9H01fHGUgeljNKNoiC7gnQ5q13Si38Y6XaBe9n_A62tWiENajyV5ch-TULakRbGnGbav6Vwg3VCYzIDVuqahoxSf_wYGyy98zWKJe126pJnIwF0NY3v7Fj-yhhAh2FWWRVWIRabvDw_dT6wq7IXo71qrp1sUejicHuAmMzLSyozUYrCqf7PNq70IV8AQEhZiuDwqDAJyh2IWK10yOYp1rjoCQlJpZhQpKmE5BbC0XWHgu9RuMmK711aRBvN_SUnAzQbv6W0VXRdGslQO2FyyefeBsCEK6r1wiOOLrgAi0CT8ElvvWp_7tKXXHog04L--CRj_yePmCQ88Mj0lLG0grU7nQxCNKofgpokVuLDI12blp8wmthqEixSPFgsm1s16V8wGBhQBtm9P8zreIBl4BfeAR5qE4Iefqoq3p_gM6diGFtxuRdg0g78k7utGHP-4bMePXbnxrflNuFSOGdkvUdSdTXZ0r6IsMIVTX9eS3trhADrZus1tZpra0d24y3ky3j34JNqiIKmOjYB_bakJzVBuTLAIvPIWgmpAaAhYSIshjMQjXptt8zlpGvITYTsca1mn4AE6nRn9_OeccduV61TtrtQbMxveHlpbInEFkI4qoeLYy-QfQ376UCH2HMmeRuJp3Wxi-S9U3w4YMtP09VXK2f_POyi8FgT2EVk26pBH_bRyO1nhh6B_ZZMDTpyCOT5KCu12W3sjTBNUBGw_9KYIfEgBpjYmq0nnt3qv2RVZLgZ47zx59LYAJtYulTk6WlTVqFUnYPFbPQvyL7hFmrl673fVVOTVL1Q1AKRZFuvfLctdiGkG4KIJ7AvrUfsJ5l0aui8RcFCESEQg45EpSlIDdKHQzkokK09M9YKpURpC9smujz4JAr02D9WhwYyOvoYWZmKeNDkF_bayFgm-Q-L41AJzz8I3vwAf6DC-mv7KAUriKwRQ5m1M_K1EXzTAE8xIf7hsvp5hJ5Ab_19rw0tAR9dJS5-u--jEyyM4lTd8yiRxgWRT9jkRCa2tbbA2SwdM63MVa5yE7roTO_35p2Rqj5kPa0OOMLXXiObDnjioo4oh282sHLrG3uE9JWX4_Vwl5s4jjPgevNtgeCgoY0k1QbGeAumpILQVkz1wO9bl6AvqPPaReuKGHFrrCGwhfYSTzs7Zb3Ps3shaL-UMXCEKuhbps3M1p_4i3nSNPFNrwzXXgwhGccAZItST8kWiDgPMFCzZ6KYJfYp2qS4GeC6BZi_hA0mTujYvcmvsozqUrXKudJ8DuVJe12iWRhScI8x2rlAzH9_5B7QfyEppUi2h3MUk14u0mZGfsJBKzYq-VySP0oM4wLug5U57uf8wETKX5uUPFDfeOMZB9sQI3yk-UK8Bu2eGr5prApUnQeEDXy-h0pdgR7ER23sy1XLNLLrUN-V49C5zVzbx_FDwRqyg_VNn20tgxYtcQR8B5fihHDDmG4fWsGaj-aXU4QJooxMrhB7caCDJRv3QnZXkEFbKR7VYT8UKiqP7SQc0-47k1FcyG8OhCZ_yZeZKZKGo5kOzAnY0IE5OGHng-olfYbe5xrxWRKPor9o0iMkMuNRpzTx_qpbFC8scoMgW-5HFF4UcQYppNSdMKV1D9QHDZfmkERqTThVXxPcTrdqBcbHafuWNcseiscgNb24sOhrY97xIMfTyFcN8hm1_-bjdc97saDMxaUjPrGeGe4hrTaaaGbQh15IXlNlNfD0y-oL81UkSqhNm-TKdt4XShgxZ8cuaUzFV-n4Rb8fNLCkTEp6OITE8psS4TrD47jqFyJLLj4yIh0GAfFsXxvPFZVc9GVVITp6JrjgQWVo3VCUjyxZXta_EuHs6NbS8n92xDCz4SpAIxsxaLrF2GEqdc0YUtxHl9l01HzoMbvZ9AkIVriQVDc-1taejJiVlcQtZD1EksucRfZT3vrOPpqI8QB9zhEV9U518hTq1elLsMovUVMhc1QJOPjC8Unf0-hxxV7Myc4tBhFo7Wj93KTHASTNryljlPBUyH1kNGbOSlax--RW8CTEqNG0GFgkaGEOtfsuYBcLMFp6BPE7QWAtaIz7wNnbNCbymqxCtSbnM3SQoOuIELRK-kUCrmeb4O_9rMpw7sU9VnrWWHbiISEEUowvw-Hh00P6yRelYeEA1YJjSnomcbEKCKIaoaeIj_zRuRDI8Sktwo7jpPkX-xwTcX9T8fZo5KF19erCg7cf5jKmGUls6eA0XvZZkE9diqQa9L1KXcyllVs8C-H0saW3HdozCOnjskxKPAIi75VKET3BipAm6uhHrS4tSPLBdcP_KoILbfnQc4cNWMZEr7dWpvNgSS3q9WVSAusB6W0Qc0Qyn-rVQ2tnx6HCmTfBO6dhCxSUMo0rhF7oelavqyaD1LNSt5u5zrI9XXcw4u6MbvlfDKiuIgHE1FcdkZuKr5S9PhnEiKzKPYmtvktPQEFb0vwY7YC9DIDDVZipup6GCNsFTCDRYGoIrZ_ZNmJGCe6CHzw1w6WuH2WEbkyyrSLcRQQjRDNodJ3FosThZlkQV7iQ6UM26KlM7-OhrJUFHk_OaxZy-P6x2aqpokhtVUpNAYVDFJ-B-BppYglZdSP-jFssxpsz97wcJsC89asjHJ-xfpRtp0H6Pi4VJMDkYKXKEyncf6qBToEn4pqedD_KcWKrbTglnIIFYYQ1W3VUmYMxkdoJBLjVmVEaxYzaBJSyPk7Y6d6IdzWNSbbJPJyFbTYG_K9BBYWVYnsoFSvjt9zero4wJnHdaArSLcfxZppjRAw7OAbFHb0IEviDWntohWT5cYOxjQPudZRaPvMJZ7PaxbgT1OJGkA7Ay3X4lXjocA3FIgvNGO2BY2xtqBZjE8q-pnSTlCPAhG1JsmUiGUlU-9DkHbAM6nCEvTDM0MV0ahW7ZNQu6-t73sDqf1gTdgBVcN1nXR86Tta2gsD_xrK2PIIQNXTBeJhyZIAUA9BvUfVYRDCyWk8m8-mGcCxy0qUkPF08Qz0VyG8xpU0IuircbF_3tUvFfhud9o1osuUlTvwJlcMWRqW13mHcoJ0XN4303T4tvJSWosK-xDINj8eJWA7X3XrRCyXFbmI0H526HDjqwSuS2DnymKHKEuWekUmFkDjAcFppisCaUHQSFU_FyMCQIqXxQMRIRCgwoK-y2e_9nEXfZpNV52C7bZcu6wCIVCmp4FThB3tutqw0G3xdrk9jXfJCY1aWq6AnO2BJFuoikkLlYVy_FFcF8hPVTVw69EGLHT43KlufyK5P8dC9CfiEsADEkcixDRJQ7pkSZ44QOznB19qZeLZcnHtMJG7n2Ht6s8e70UNWy9xokY78FVgXwz27jsQi-Dhdl-SuxK_PCVePzIPf11qinSpd1qNd0LC8c206Fps8gGiPrcXRIdYVYH3aecKUvxnS5jZOR0wZY7o-_7Qwy0Dwu5ArFQ_knOZ_i6eETA1I1o7AgpAptrBLrcfEAm0Q1xc5HTIRcFTHToHFMiFc8itoRou-2oGR5DEMH8wrBYS4sS029lP23AeoE2K4-eiHvwV-wHJAOtkhV7X12WWFmOkXBaoI_-1s11es6OqfbnW8V_XRRukgb1q8Z4fwWImJO93n84teW8Di5FrXXenUMzSL3ST4I_oNX_EGxERXm0ih5iOWoA7yihhBQwtzbHcYFHKuLSVNpS9IT1_fKFXKsLNll8AA-XxlLJHyfyajI5AJ_Yhw2gNV6g9251XqhQmrRGV9W_lvA-UoNM_kR7FbcsCpc7P-mv3tSUWNpcOIFAXz1megCmmB0EgJN2yYpL8Gzk8nulsLxVQnayhE80RrWOTWdKgWvnHH9IY7UCby6_d1rYhWQvwuXar8j4CNhGeZDRMrKHWiWzQTRJiVne8V6rpzhDItCeqRIMz717olEslnQ5hBiGd61L9G3cQqAPFuLIBtvO3qv8NRgzy12gSmLHxWtOfrtRuwJHkB-v9maNfUMDTWHaL4uKmiaB6w3qQhg5aQRcejI-6ofxRnnWyzDZIXgq9InkLrETUtV7xzwPghJvhfAAPYojNQSZyZgq-Tg8VqTZ0RJmbhPT-mvPbbETvRPUNz559bKs7Zz6A4p_dFBrBdz_shf_G65UzwJyvtLWAPO0rfbIYBhLQgzHldulZwQEyAOI5VNSRqhsYNETd-GEKSeWuc7HiX9LEolJnjwTrQV7Bw97j9TtMXLvfneu4u4afEqrRs0H-h51-MiBedxVQ9x9Vh2eEkMtrKXjvOl3DbiNUqFAgn9RugWEYbY7iwsmyPXL5M58sDudEbxbw1NixRC_RYlWKL2jrFXBhui16ONP_kLBINOJniOcpIgkDZxEKPf6EVQotkhMpUt_UWjKTjvzmdQ2qhIUyhrMWnk732Vriv7ETw8oo-OXXRpS02QraYmdtwnwJTxUj6uPAxCWv52no21xf1ORkga4Rap42A9GYFQJwXWrYlJdMgDlhrQCg8cLtCu9wxeNjTUDDqCi6p9pO1GKtmwms-4A1-mu09-eLIrwGnSewNjLmzm56d1ymu0mwTGLAazMMj8GVleFuJpWzN24YqQAwg_t5Kz5OVOwCBWJT2I7ILBO-x3eJueN-WGwX2MRcSaDAQF3XclfAX-T9OCRkGQYpVai7LdSuDUDk7OP_3XsVBPdqq9Xh_hwPXr-MJyFiAvUxNvk7Nyu2Ym_JscIibqpaME27dOlNo_AdQ9lKbPPnm08T_ahi3BuF3B9__1AWxf_kob52yOUCeikWS7xvXLJy_q9I_GXM-haFA9pS7mCIQ0pkDcakL9RwbEyrQoYzCwRTyxp8gHickGRD-DWJpQ8Y-8Zfvnw9cesGwmkK5jkSpunoH06VM45TdBzdVR7YKMlS2LLhgNQxPQoKz8c1M6C01W4BHp4KMJ7uQrV1IW6Xl0fKxuHdOOSOxb8YXlA1TVILq4f_M3O4NlIhsY18haQ7h-1duMlZWwy-2iBgSUdAeMqmbla9i0mPcHk01JukjvyV_TEk6zZWmuxOepKEjGu8X1PujyEExVKYa8SA00YFDoFPNA5_6fptmDeuo3sIrhl5Pfu_BGpj.6Jk_2vU-opZe2On_y6353g'

const STORE = 'ITAPEMA - SC';
const MATRICULA = '011177'; // ⚠️ IMPORTANT: This matricula must have access to the STORE above.
                            // If you get 0 rows, it's likely because this matricula is restricted to another unit.
                            // Find the correct matricula in the Network tab (Payload → matricula) when viewing the store in the browser.

// ═══════════════════════════════════════════════════════════════
// FULL STORE LIST (for reference — copy/paste into STORE above)
// ═══════════════════════════════════════════════════════════════
// AHU - PR                              ALMIRANTE TAMANDARE - PR
// ALPHAVILLE BARUERI - SP               ALPHAVILLE MONTREAL - SP
// ALPHAVILLE MURANO - SP                ALTO DA XV - PR
// AMERICANA - SP                        ANAPOLIS - GO
// APUCARANA - PR                        ARACAJU - SE
// ARACATUBA - SP                        ARAPONGAS - PR
// ARAQUARI ITINGA - SC                  ARARAQUARA - SP
// ARAUCARIA - PR                        ARIQUEMES - RO
// ASSIS AV D ANTONIO  - SP             AVENIDA DAS TORRES - PR
// BACACHERI - PR                        BAGE - RS
// BALNEARIO CAMBORIU - SC               BALNEARIO PICARRAS – SC
// BALSAS SANTO AMARO - MA               BARRA DO GARCAS - MT
// BARREIRAS - BA                        BATEL I - PR
// BATEL II - PR                         BAURU - SP
// BELEM - PA                            BENTO GONCALVES - RS
// BH - BELVEDERE - MG                   BH - LOURDES - MG
// BH - LOURDES II  - MG                 BH - RAJA - MG
// BH ANCHIETA - MG                      BIGUACU - SC
// BLUMENAU - SC                         BRACO DO NORTE - SC
// BRASILIA - AGUAS CLARAS - DF          BRASILIA - DF
// BRUSQUE - SC                          CACADOR - SC
// CACOAL CENTRO - RO                    CAJURU - PR
// CAMACARI - BA                         CAMBE CENTRO - PR
// CAMBORIU - SC                         CAMPINA GRANDE - PB
// CAMPINAS - NOVA CAMPINAS - SP         CAMPINAS - SP
// CAMPINAS TAQUARAL - SP                CAMPO GRANDE - MS
// CAMPO GRANDE - RJ                     CAMPO GRANDE CENTRO - MS
// CAMPO GRANDE II - MS                  CAMPO LARGO - PR
// CAMPO MOURAO - PR                     CAMPO VERDE - MT
// CAMPOS DOS GOYTACAZES - RJ            CANOAS - RS
// CANOINHAS - SC                        CAPAO DA CANOA - RS
// CAPAO RASO - PR                       CARAMBEI - PR
// CARUARU - PE                          CASCAVEL - PR
// CASCAVEL CENTRO - PR                  CASTRO - PR
// CAXIAS DO SUL - RS                    CHAMPAGNAT - PR
// CHAPECO - SC                          CHAPECO EFAPI - SC
// CIANORTE - PR                         COLOMBO - PR
// CONCORDIA - SC                        CONSELHEIRO LAFAIETE - MG
// CONTAGEM - MG                         CRICIUMA - SC
// CUIABA - MT                           CURITIBANOS - SC
// CWB - AGUA VERDE - PR                 CWB - CENTRO - PR
// CWB - ESTACAO - PR                    CWB - FAZENDINHA - PR
// CWB - PINHEIRINHO - PR                CWB - UBERABA - PR
// CWB - XAXIM - PR                      DOIS VIZINHOS CENTRO - PR
// DOURADOS - MS                         DOURADOS JD SÃO PEDRO - MS
// ECOMMERCE ADEMICON                    ECOVILLE - PR
// ERECHIM - RS                          FAZENDA RIO GRANDE - PR
// FLORIANOPOLIS - CAMPECHE - SC         FLORIANOPOLIS - SC
// FORTALEZA - CE                        FORTALEZA EDSON QUEIROZ - CE
// FOZ DO IGUACU - PR                    FRANCISCO BELTRAO - PR

// ═══════════════════════════════════════════════════════════════
// ENDPOINTS & PAYLOADS
// ═══════════════════════════════════════════════════════════════

const ENDPOINT = '7a8110990e16404daec259c355434bc6.pbidedicated.windows.net';
const PATH     = '/webapi/capacities/7A811099-0E16-404D-AEC2-59C355434BC6/workloads/QES/QueryExecutionService/automatic/public/query';

function buildPayload1(store) {
  return {"version":"1.0.0","queries":[{"Query":{"Commands":[{"SemanticQueryDataShapeCommand":{"Query":{"Version":2,"From":[{"Name":"m","Entity":"1_Medidas","Type":0},{"Name":"t","Entity":"tbl_cotas","Type":0},{"Name":"r","Entity":"Regiões","Type":0},{"Name":"p","Entity":"Parâmetro_Senhas","Type":0},{"Name":"a","Entity":"acessos","Type":0}],"Select":[{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Produção Oficial"},"Name":"Medidas.Produção Oficial","NativeReferenceName":"Produção Oficial"},{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Retenção"},"Name":"Medidas.Retenção","NativeReferenceName":"Retenção"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"id_consultor"},"Name":"tbl_cotas.id_consultor","NativeReferenceName":"CNPJ"},{"Column":{"Expression":{"SourceRef":{"Source":"r"}},"Property":"regiao"},"Name":"Regiões.regiao","NativeReferenceName":"Região"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"Consultor_Matricula"},"Name":"tbl_cotas.Consultor_Matricula","NativeReferenceName":"Consultor"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"},"Name":"tbl_cotas.nm_unidade_bi_original","NativeReferenceName":"Unidade Original"}],"Where":[{"Condition":{"Comparison":{"ComparisonKind":1,"Left":{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Produção Oficial"}},"Right":{"Literal":{"Value":"0L"}}}},"Target":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"id_consultor"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"Consultor_Matricula"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"}},{"Column":{"Expression":{"SourceRef":{"Source":"r"}},"Property":"regiao"}}]},{"Condition":{"Comparison":{"ComparisonKind":1,"Left":{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Filtro de Cotas"}},"Right":{"Literal":{"Value":"0L"}}}},"Target":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"id_consultor"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"Consultor_Matricula"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"}},{"Column":{"Expression":{"SourceRef":{"Source":"r"}},"Property":"regiao"}}]},{"Condition":{"In":{"Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"Grupo Ativo"}}],"Values":[[{"Literal":{"Value":"'Sim'"}}]]}}},{"Condition":{"In":{"Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"}}],"Values":[[{"Literal":{"Value":`'${store}'`}}]]}}},{"Condition":{"And":{"Left":{"Comparison":{"ComparisonKind":2,"Left":{"Column":{"Expression":{"SourceRef":{"Source":"p"}},"Property":"Parâmetro_Senhas"}},"Right":{"Literal":{"Value":"929009D"}}}},"Right":{"Comparison":{"ComparisonKind":4,"Left":{"Column":{"Expression":{"SourceRef":{"Source":"p"}},"Property":"Parâmetro_Senhas"}},"Right":{"Literal":{"Value":"929009D"}}}}}}},{"Condition":{"In":{"Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"a"}},"Property":"matricula"}}],"Values":[[{"Literal":{"Value":`'${MATRICULA}'`}}]]}}}],"OrderBy":[{"Direction":2,"Expression":{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Produção Oficial"}}}]},"Binding":{"Primary":{"Groupings":[{"Projections":[0,1,2,3,4,5],"Subtotal":1}]},"DataReduction":{"DataVolume":3,"Primary":{"Window":{"Count":500}}},"Version":1},"ExecutionMetricsKind":1}}]},"QueryId":"","ApplicationContext":{"DatasetId":"ecfe45f3-4e9f-4a6e-9d56-91020972365d","Sources":[{"ReportId":"0fdd545d-8c7f-4a8a-9723-daf16cac10d6","VisualId":"081f7f5c60a98980243b"}]}}],"cancelQueries":[],"modelId":5258155,"userPreferredLocale":"en-US","allowLongRunningQueries":true};
}

function buildPayload2(store) {
  return {"version":"1.0.0","queries":[{"Query":{"Commands":[{"SemanticQueryDataShapeCommand":{"Query":{"Version":2,"From":[{"Name":"2","Entity":"2_Medidas_Tabela","Type":0},{"Name":"t","Entity":"tbl_cotas","Type":0},{"Name":"m","Entity":"1_Medidas","Type":0},{"Name":"p","Entity":"Parâmetro_Senhas","Type":0},{"Name":"a","Entity":"acessos","Type":0}],"Select":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"versao"},"Name":"Sum(tbl_cotas.versao)","NativeReferenceName":"Versao"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_consultor"},"Name":"tbl_cotas.nm_consultor","NativeReferenceName":"Consultor"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"id_matricula"},"Name":"tbl_cotas.id_matricula","NativeReferenceName":"Matricula"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_pv"},"Name":"tbl_cotas.nm_pv","NativeReferenceName":"PV"},{"Aggregation":{"Expression":{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"vl_credito_venda"}},"Function":0},"Name":"Sum(tbl_cotas.vl_credito_venda)","NativeReferenceName":"Crédito Venda"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_producao"},"Name":"tbl_cotas.dt_producao","NativeReferenceName":"Dt Produção"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_venda"},"Name":"tbl_cotas.dt_venda","NativeReferenceName":"Dt Venda"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"categoria_consultor"},"Name":"tbl_cotas.categoria_consultor","NativeReferenceName":"Categoria"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"cd_ponto_venda"},"Name":"tbl_cotas.cd_ponto_venda","NativeReferenceName":"Cód. PV"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_cancelamento"},"Name":"tbl_cotas.dt_cancelamento","NativeReferenceName":"Dt Cancelamento"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_atual"},"Name":"tbl_cotas.nm_unidade_bi_atual","NativeReferenceName":"Unidade Atual"},{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Obs Cota"},"Name":"1_Medidas.Obs Restrições Cota","NativeReferenceName":"Obs Cota"},{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Produção Analitica"},"Name":"1_Medidas.Produção Analitica","NativeReferenceName":"Produção Analitica"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"rn"},"Name":"Sum(tbl_cotas.rn)","NativeReferenceName":"id_bi"},{"Measure":{"Expression":{"SourceRef":{"Source":"2"}},"Property":"Cota"},"Name":"2_Medidas_Tabela.id_cota","NativeReferenceName":"Cota"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"pz_cota"},"Name":"Sum(tbl_cotas.pz_cota)","NativeReferenceName":"Prazo Cota"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"pz_comercializacao"},"Name":"Sum(tbl_cotas.pz_comercializacao)","NativeReferenceName":"Prazo Grupo"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"tem_pagamento"},"Name":"tbl_cotas.tem_pagamento","NativeReferenceName":"Tem Pagamento?"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_contemplacao"},"Name":"tbl_cotas.dt_contemplacao","NativeReferenceName":"Dt Contemplacao"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"},"Name":"tbl_cotas.nm_unidade_bi_original","NativeReferenceName":"Unidade Original"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"qtd_pc_atraso"},"Name":"Sum(tbl_cotas.qtd_pc_atraso)","NativeReferenceName":"Qtd Parcelas Atraso"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_plano_venda"},"Name":"tbl_cotas.nm_plano_venda","NativeReferenceName":"Plano Venda"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_situacao_cobranca"},"Name":"tbl_cotas.nm_situacao_cobranca","NativeReferenceName":"Situação Cobrança"}],"Where":[{"Condition":{"Comparison":{"ComparisonKind":0,"Left":{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Filtro de Cotas"}},"Right":{"Literal":{"Value":"1L"}}}},"Target":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"versao"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_venda"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_producao"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_cancelamento"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_contemplacao"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"id_matricula"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"categoria_consultor"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_consultor"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"cd_ponto_venda"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_pv"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_atual"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"tem_pagamento"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"pz_cota"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"pz_comercializacao"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"qtd_pc_atraso"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_situacao_cobranca"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_plano_venda"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"rn"}}]},{"Condition":{"In":{"Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"}}],"Values":[[{"Literal":{"Value":`'${store}'`}}]]}}},{"Condition":{"And":{"Left":{"Comparison":{"ComparisonKind":2,"Left":{"Column":{"Expression":{"SourceRef":{"Source":"p"}},"Property":"Parâmetro_Senhas"}},"Right":{"Literal":{"Value":"929009D"}}}},"Right":{"Comparison":{"ComparisonKind":4,"Left":{"Column":{"Expression":{"SourceRef":{"Source":"p"}},"Property":"Parâmetro_Senhas"}},"Right":{"Literal":{"Value":"929009D"}}}}}}},{"Condition":{"In":{"Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"a"}},"Property":"matricula"}}],"Values":[[{"Literal":{"Value":`'${MATRICULA}'`}}]]}}}],"OrderBy":[{"Direction":2,"Expression":{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_producao"}}}]},"Binding":{"Primary":{"Groupings":[{"Projections":[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22],"Subtotal":1}]},"DataReduction":{"DataVolume":3,"Primary":{"Window":{"Count":500}}},"Version":1},"ExecutionMetricsKind":1}}]},"QueryId":"","ApplicationContext":{"DatasetId":"ecfe45f3-4e9f-4a6e-9d56-91020972365d","Sources":[{"ReportId":"0fdd545d-8c7f-4a8a-9723-daf16cac10d6","VisualId":"cc96a767b12339a67c49"}]}}],"cancelQueries":[],"modelId":5258155,"userPreferredLocale":"en-US","allowLongRunningQueries":true};
}

// ═══════════════════════════════════════════════════════════════
// HTTP POST
// ═══════════════════════════════════════════════════════════════
function post(payload) {
  return new Promise((resolve, reject) => {
    const data = JSON.stringify(payload);
    const req  = https.request({
      hostname: ENDPOINT,
      path:     PATH,
      method:   'POST',
      headers: {
        'Authorization':  TOKEN,
        'Content-Type':   'application/json',
        'Accept':         'application/json, text/plain, */*',
        'Origin':         'https://dashboardbi.ademicon.com.br',
        'Referer':        'https://dashboardbi.ademicon.com.br/',
        'User-Agent':     'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36',
        'Content-Length': Buffer.byteLength(data),
      }
    }, res => {
      let raw = '';
      res.on('data', c => raw += c);
      res.on('end', () => {
        if (res.statusCode !== 200) {
          console.error(`HTTP ${res.statusCode}:`, raw.substring(0, 400));
          return reject(new Error(`HTTP ${res.statusCode}`));
        }
        try { resolve(JSON.parse(raw)); }
        catch(e) { reject(new Error('JSON parse failed: ' + raw.substring(0, 200))); }
      });
    });
    req.on('error', reject);
    req.write(data);
    req.end();
  });
}

// ═══════════════════════════════════════════════════════════════
// DSR PARSER
// Power BI compresses data using:
//   ValueDicts — lookup tables (D0, D1...) for string values
//   DM0/DM1   — data rows where:
//     C = new values for this row
//     R = bitmask of which positions repeat from the previous row
//     S = schema row (defines column order) — skip for data
//   G0 = direct value on the row object (no dict, no C array)
// ═══════════════════════════════════════════════════════════════
function parseDSR(data, label) {
  fs.writeFileSync(`./raw_${label}.json`, JSON.stringify(data, null, 2));

  const result = data?.results?.[0]?.result?.data;
  if (!result) throw new Error(`[${label}] No result.data`);

  const descriptor = result.descriptor;
  const ds         = result.dsr?.DS?.[0];
  if (!ds) throw new Error(`[${label}] No DS[0]`);

  // Friendly names from descriptor Select array
  const selectItems  = descriptor?.Select || [];
  const friendlyName = {};
  selectItems.forEach(item => {
    friendlyName[item.Value] = item.NativeReferenceName || item.Name;
  });

  const dicts = ds.ValueDicts || {};
  const ph    = ds.PH || [];

  const allRows = [];

  for (const group of ph) {
    // Handle both DM0 and DM1 groups
    const dmKey  = group.DM1 ? 'DM1' : group.DM0 ? 'DM0' : null;
    if (!dmKey) continue;

    const entries = group[dmKey];
    let schemaRow = null;
    let prev      = [];

    for (const entry of entries) {
      // ── Schema row: defines column order ──────────────────────
      if (entry.S) {
        schemaRow = entry.S;
        prev = new Array(schemaRow.length).fill(null);

        // Handle case where schema row ALSO contains data (G0, G1... keys)
        // e.g. { S: [...], G0: "AHU - PR" }
        const hasDirectData = schemaRow.some(s => entry[s.N] !== undefined);
        if (hasDirectData) {
          const row = {};
          schemaRow.forEach(s => {
            const alias = s.N;
            const dictKey = s.DN;
            let val = entry[alias] ?? null;
            if (dictKey && dicts[dictKey] !== undefined && val !== null) {
              val = dicts[dictKey][val] ?? val;
            }
            row[friendlyName[alias] || alias] = val;
          });
          allRows.push(row);
          // Update prev state
          schemaRow.forEach((s, i) => { prev[i] = entry[s.N] ?? null; });
        }
        continue;
      }

      // ── Direct value row (G0: "value" style, no C array) ──────
      // Used in DM0 store list queries
      if (!entry.C && schemaRow) {
        const row = {};
        schemaRow.forEach((s, i) => {
          const alias   = s.N;
          const dictKey = s.DN;
          let val = entry[alias] ?? prev[i];
          if (dictKey && dicts[dictKey] !== undefined && val !== null) {
            val = dicts[dictKey][val] ?? val;
          }
          row[friendlyName[alias] || alias] = val;
          prev[i] = entry[alias] !== undefined ? entry[alias] : prev[i];
        });
        allRows.push(row);
        continue;
      }

      // ── Normal C-array row with optional R bitmask ────────────
      if (entry.C && schemaRow) {
        const C        = entry.C;
        const R        = entry.R || 0;
        const resolved = [...prev];
        let   ci       = 0;

        for (let pos = 0; pos < schemaRow.length; pos++) {
          const repeated = (R >> pos) & 1;
          if (!repeated) {
            resolved[pos] = C[ci] !== undefined ? C[ci] : null;
            ci++;
          }
        }

        for (let i = 0; i < schemaRow.length; i++) prev[i] = resolved[i];

        const row = {};
        schemaRow.forEach((s, i) => {
          const alias   = s.N;
          const dictKey = s.DN;
          let val = resolved[i];
          if (dictKey && dicts[dictKey] !== undefined && val !== null) {
            val = dicts[dictKey][val] ?? val;
          }
          row[friendlyName[alias] || alias] = val;
        });
        allRows.push(row);
      }
    }
  }

  return allRows;
}

// ═══════════════════════════════════════════════════════════════
// CSV WRITER
// ═══════════════════════════════════════════════════════════════
function toCsv(rows) {
  if (!rows.length) return '';
  const headers = Object.keys(rows[0]);
  const esc = v => {
    const s = (v === null || v === undefined) ? '' : String(v);
    return s.includes(',') || s.includes('"') || s.includes('\n')
      ? `"${s.replace(/"/g, '""')}"` : s;
  };
  return [
    headers.join(','),
    ...rows.map(r => headers.map(h => esc(r[h])).join(','))
  ].join('\n');
}

// ═══════════════════════════════════════════════════════════════
// MAIN
// ═══════════════════════════════════════════════════════════════
async function main() {
  // Sanitize store name for use in filenames
  const storeSlug = STORE.replace(/[^a-zA-Z0-9]/g, '_').replace(/_+/g, '_');

  console.log('═══════════════════════════════════════════════');
  console.log('  Ademicon Power BI Extractor');
  console.log(`  Store : ${STORE}`);
  console.log(`  Matricula: ${MATRICULA}`);
  console.log('═══════════════════════════════════════════════\n');

  // ── Query 1: Summary ─────────────────────────────────────────
  console.log('▶ Query 1 — Summary (Produção Oficial por Consultor)...');
  const raw1   = await post(buildPayload1(STORE));
  const rows1  = parseDSR(raw1, 'q1_summary');
  const file1  = `./output_${storeSlug}_summary.csv`;
  console.log(`  Rows  : ${rows1.length}`);
  if (rows1.length > 0) {
    fs.writeFileSync(file1, toCsv(rows1), 'utf8');
    console.log(`  ✓ Saved → ${file1}`);
    console.log(`  Sample:`, rows1[0]);
  } else {
    console.log(`  ⚠ No rows. Check raw_q1_summary.json`);
  }

  console.log('');
  await new Promise(r => setTimeout(r, 800));

  // ── Query 2: Detail ──────────────────────────────────────────
  console.log('▶ Query 2 — Detail (Tabela de Cotas)...');
  const raw2   = await post(buildPayload2(STORE));
  const rows2  = parseDSR(raw2, 'q2_detail');
  const file2  = `./output_${storeSlug}_detail.csv`;
  console.log(`  Rows  : ${rows2.length}`);
  if (rows2.length > 0) {
    fs.writeFileSync(file2, toCsv(rows2), 'utf8');
    console.log(`  ✓ Saved → ${file2}`);
    console.log(`  Sample:`, rows2[0]);
  } else {
    console.log(`  ⚠ No rows. Check raw_q2_detail.json`);
  }

  console.log('\n═══════════════════════════════════════════════');
  console.log('  Done!');
  if (rows1.length) console.log(`  • ${file1}  (${rows1.length} rows)`);
  if (rows2.length) console.log(`  • ${file2}  (${rows2.length} rows)`);
  console.log('═══════════════════════════════════════════════');
}

main().catch(e => { console.error('\n✗ Failed:', e.message); process.exit(1); });