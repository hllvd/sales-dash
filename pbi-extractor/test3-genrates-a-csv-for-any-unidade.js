// extract_ademicon.js
// Run with: node extract_ademicon.js

const https = require('https');
const fs    = require('fs');

// ═══════════════════════════════════════════════════════════════
// ✏️  CHANGE THESE THREE VARIABLES BEFORE RUNNING
// ═══════════════════════════════════════════════════════════════

const TOKEN = 'MWCToken eyJhbGciOiJSU0EtT0FFUCIsImVuYyI6IkExMjhDQkMtSFMyNTYiLCJraWQiOiI3NjlGODk2NEQxNzA4Q0VCMDgxQzU4MTBGQ0I2RUJERUZFRjcwRUU4IiwidHlwIjoiSldUIiwiY3R5IjoiSldUIn0.Fzts0cBkAq8Sb3ruslXifGWwD2G93aQk5KqZNkOXa5M0xIjM2XAxhVadsb08oi9Tzv0Ej4mEY-qbp4yVS5V81RBPdxJ75hI4J_9oTHFDNwgvYCABRBFHvTG791nWZIXnoYml1WnTxXNduPKOwgPQY1yvnTEGz_GvulCnD4L2TyF_1XgHPV65NqmH4p3xeI_39JyssHdGPrK2Qlc17lrrGqKLZYlqTJznXTdKIm_UB9Ivu507gPPvVs4pjBK6toT0WbMSR8L_0x9gI53QaNdPfQCT_JEXckLFNJ2QysWKPMemRkakZNLlynYFgJ-H8-9Rj1Wavp5e7aDarX4efVzpiQ.sUzC8gdW703CkhL6vcmlCQ.t8-AVuDqL2E6cOib9h4HOw1uRMw9okdcz9poC2SjO1Soi_kTXcUrvSUyKpOiPSMzWYV6j6Q2I7gdA66kzAHDamX5IHWlLMHAhcfxfFL4t9ZhdoWRafxXz1xDYEzmQozW4PzKKZCBHrBLvRxfzfBHM55sAo9M4ZX_oxYjInmIiuBbSRLuGWt4oJ1vji8JHpB3iUYiAHY4sB56n4lSejFhSwOiP8Y836Yh3MaZqv9J5-o04dm2Y8dSCtb_KtgHxE7QpYZ5JmE7DNkidQFN70VcKMSXRj6fpzursF_JunZv9rqrm5YhlPlPiLUrAKZ2llg-cGEzQUg_Yvvf9yGXGjxMf1aFrIDkF3at_CcOgblIyBuSWxKNWdtKVOS3bmgGNnufUxnSmV2r03s6wn5dp9drncosDJuKc0v9CYfWG-PT8taScpq_ne3tWZbVTxZ_VG-QRPvQz34kroJjL-MeOzUjpYW_BytbFkRWTuZP-8vRGwnOlSU2taEpdRXSme_5dsydhwQnDtHJI8-y0BlnQU6PpNKEqmBP156-fqK9VUG5RqYFzwHryzm9UrMVZKnWomXAAHCNk8A37OM4BNKngGjTyGgris4a18Z0LYupEBJmxpVPfZdxBcKCSS9bRLRxwRhx4M1MGkyc3OI0QUjgE_D6n4u007qeIYmhp9Imgsb2vIpVHAHduV7rxHNS0EQWNQgsxR4I2gtOnHbAES5Pknc8wJvl5kR_zGqdsBDKlb-JqNvd0VSPHjxIVmgbFug72caGuRyrZWbsHFfzSKoEa4n8ofTInaUElY7leGwnfhqfIe-SPskX5HJtQWFpZvwM_g-vmRJLuen--wJDMolVefah1Bb-uVfFMK0cRSUa0-djZLRLG4BbCSBB52zqtG5KilptXzgTSQE2wABDLs2M_vwj7udVkex6P6own5Fk6urDBO4WUjNtWFwY-kJ5SXPXhh6L05mrtfnQ4WZTyRvGBb3mCyqkXtnAlz27HNOuSbVz_reK853wzJQXYMo8BEXYeU8l6b0dTXNGoSFM03Q919n7NNftomXUlsRyfzgH08ttOLo6DgGZA7HXquVdG2XQWe-fLxVIYhkOJZ8bW2rjdGZnW5cmYm-4w9-8vVDBGvd5KPDpDUVgWJrEAvNRy_BWJTBzlF8huSPTWdlcsJCKHzlcKCNbG1EwAsanCmunFDj-94eHb-Dj0kWu1CSoMONCZoiGDydoFq2EIqegR61-z_Ke5wlUL04EWq4n6vf98vFFh4ZSgAiO5dTjqy6CvGYHiqf6eq03BLaKlT12X4NsAQv0sYP21QdDSLTGqW342nt0_IadJMppQxKJice04dE3UjlJkIuD5h_gL7l87RYUCPxHLzi1njx06XaojmUh-nNzOmIWYuTiasxS6pdPwdjuUz3TybuaFazHLXZALG5w0wNvNWEFDT_d7PpFTft7o83MzCjVLU0Y8rYlU7rD2dyBzOSIQjDKCJ1pOZlmdiBJu_JNFDLhr1oa2qraZMW8Wx-ZC5rCA_BOEGup2oJCt3Vn0uQkK4i8Zedhv5RIjdO0Z8g8iXCtTtArhp7GdC58gqNIBhnA4mTPhG8m0ctjD25VWC6nGFv4Em-h1xRL0UakHYwVZaiHcLMU3gsJNJvUr5s0eCTMcixj1txUjVYmYw4VyaY4LxfuW3N8Kq4zFfxaq_ORtaEx65jAEDwpFJdLTHs2wX04Xaf1WDvlrnpZ8qwQABKSBnIhKt71XXEb2caSPCout3AFCLqjCIXkPRlMgM4ER-xR5t-qxL_KMtqcVr_jc9mqux5Wy0PnM2p3qkR9smqaJUqhXvzZwQ34VnexEtRK6WBCTf9xcTbQOPJjtNjll2LpUj_-BM2gEYHQ8rEyo1YuJZ7Rf-GyWaszFcGZi3sAC_4HzKV074-phwuPsnxnKNe6b4Qud4KlMtYfd3McP4myrXExppOB_WUNVd_UVkkAmKivPp45TcUPrn3QyoP3cOLZFFVgWrws5kDi_1gwveqcMpS5dKb5ZspW3YWFwHcGmhcYQ5Atb3eOj90XdOt1EN91yaEGjpLr0zXZDC8VH5PWNHIleRo6upRm4mtQxxvw-d7GTMrDssF6BmjDtLszVcz7oWJL3N8MQUzfulAyf-eG0Uyi_AYPR44b6EXzOkkNNXayHFqcC7phYXnVpG0Y0GiNBLLXfAceecuwAinai8BvpMGyiCZgsYXDhG-TjHrYTzqKC9F0aHKry7n6nJwPwZ2LrZpnP7aLJi5-uZG_gpOdOWm0c2Mf69Wzpx_7tyxwmeCJZ9yi3vAk_xhAKhRyk5Q2YkfTqQX9XMS_DG5Cl4ynMKs8cRCAGGmAjeZMFp7ApMTz050h9_AJo4eCQ_pj-vWhKM1wUEGpB_2KFO6Y3ewaxK2AnvNoipWhuD7M-tF86Z-YCMHkB5PmXLWbrYO-lHVeA9z5bGwBSZSniR0aQ6mGanAmEJYM29jcevoejiwzPwJZsgB2-CB1tvmvRZqxMnRiUnzezjNzQD13ENK4CpUGSg9JtQ2-Dk5RroaPiRz24nnds2feYzyMniBqttkXHaeOcW_6v3Ib2oI9yQ5HIyDoG38xsi5aFMDkoYCxhiXWpsYUXDOHwK3_eSG3587biX0IBwkdOpiJt_K-SA3dA_oq5bKz-c9w2Ab_VOt-N4ZNZ89WO9LgUNw79Fez02ArS47UkchdMeqxr1K_64H8pjFaF_rYBQcLA-9DhUs7Sm0iOfUJTO5q_LpeKARSJ5a8svpx14GeSdoUK0N6SqfSGhwGtvST9OW0p1HnKLcEh4p7bYmdagx0cDesa4jvyy21FKnEjBSmLbmDG_6NncZjayOP9GoamA1PoalTcMnFZwyzos_dJZsSpCP3J2KIZyKYLWk8xiYXzdJFnQQspRVX9jGaZ0SHwE_jJYrecZPbz7tzO0n6CHkP9Dy7ND34scJ_-khUnkV8OgS9-JWbAL6FPTGWdRcCXcpNBwMPsoYZ2sxtaoTv63plc58VaI4YfpT6jMBCwwPxvMUKBV2pncJOQLON_zxOutw41zie1lLFFe7Gu31vewV_8iHvX9MovzK20lJUwA2WqBGCEIS7Z4SXbpaiyGki_MowdruwPliZzrwABAaMotHBy090ae7nv-M4ZebusWfSv8FkKGOMONDiZwlPlrg2gZNft6dbKbP21wCaooZP0EQOq9n2StxOVFnI_gBs9pHOrgx0WEBrM2ZJIRpqyiLbA7vell-jIh0MsKjSMKUmXnOks19Q7PfpUWuFrAYyd4ZgHO2Vc-xmTH-Qv_5KIo9GDyqKYIIZbhCNHXYWfSyzbJoqeqQ6OYlATZzstPuBXBQwgR8mO8t7ogpmKoEnkBnArJlmDhSsVsI9AoWe4p-dPrsYXQBOLRSS1bzAva2Ui7M1vUmZ5i0disS-xu9wO-o3X37m1hThaH64oYptTTwpzFhs6Eo3Hg4pbdolSOczo8Js4tKgRQzYQ0u2dYm8ztp_in6oWLRk6lk3uptaTOOy6pSCoNUkw1JFp0EKTryTzSoqwNE7o7zFrUJYK9vS0OHRkqkyu_wgulKtpCxhHiFOsiCqk-EndZPYqMssBo7V6ZtOk_7o_QC05A-w_Qpx_MLAvz37bYYouzq780MYdYsb7BWeN7U7QjWRvFcj7lRFkkvHqC4C23zalbJ64aCY_AmoKJvbLWUIb56Vo5e4lEuRwExIZ9VSIlVrnmtQ2qEZcd6xa0ZAFFupnO9o3arP7QqHUMiUdbe5iqeSkLh1IhZiDzMAJBAtTz3EuwQRTvAq1BI4r8_cqrPWtc1PPjgdy_f-4DcwYaPHgC7w6UIOGSK9z4lTGOTSWCe7cFZ3B5bhK_CQQW4VHh_z1yGLSXBV1eYWVv6iHq__lsh2i7gLoit7c7kYbF59pV647jgsADu5kepaeYu87Dng-PCyorSkXRSaA7Dlhk8Gsbejvt9sP5mXMNjWHVNyB2LAJudFmdt1cWFJiCndadeQwike0r3k_rO1TUIBSJuiTWnoR1qaxPalwGLw3u3VcpMhL_aptAieodFjdUHb3dzJ8cu_cRwxggJmIERMW9m0MqrRsFC4W4sM-S2uanFE8gKEFvbU8m2VsTd_P1R3IEiVJK58olRnCX_M_pub7pgEmtCb0LYdsJHpapuDf4bz8VaYBT5UAfpJ0DRCCOy6sO7ch6OfLDpBOG4ktDS5GQpWP5vPeWN8O95x6pJI1HY0NCixGkPiSywGIdu369sliYfu1zRqhk-MjSEnnvpC3TOOVLeuyvLEiikiB5Wab0CuWFi2C2otiwvcSSx8GbFsmn86e_M2X6FiPYRB4EfyQjiuftcS_u7vjcQ99X1uqdTXFDWLr1yvr5Qw7cNyP4J2AsJ96Hh1IdDIiJA2gkFUyy4P_BAU3iGB4qwEarIJY2b5CKBn3fzdDOmhfgO91oazQdXqCyeFHOH_SCdPTVVxaYIGcX-E0oATs4IFhvH8mYFu33LgClhkzLgA6c4Ef6vT6Rvq8BlIHTzRgWJ6Lg_VGVqHOd4bgx4_wEZMU3B_ZKGs0O0pRbhjs5K90EO0RY2NRrlYrwbSW1JSO4o-aNrfwjvtuWwFXWRb8iLez4NqKOmKuala44S5r4cExo5xDYjO8bHbpCYIC9jUTbx82peiwWqnuwsFe807OJTeG3qt7b1WNsUz-YcxN9zJNpbIzELjVxhUl9613ZuI7gVL00CzuZdgV1bxJI0tCw95Em34ejVxNt7Qtjtr7CwW92Xp-TEYL3WH22nCUxm6Js6fdunKzE6IUxkOfwfXBK5HcCRxljJRdspfOi9ztZHV7zM5pcNroIjeFUWPmJvXl2KLHywcgVdmTcXtZZOwJzZty0EjueeGrPVK9Xbu3RylFy5tCixeItdVZZHWJxHQn3m4Azx_g165Kl11c8niN5dnfLJ0ObTipVVOQGZ9MRB1aLja6Yp2tqnkq5HSYlo3cEpANu7867yd7p_EJaRHMPCvC3s8MEdYvULAy-FccZ3J9cF4iikEXyKdYRJmR9ODr8jyHJDsG-Su2A5pQwG-5wKfIGF3zVizsRelc3D1NL0Vrs0P3CEWS9GTAsYgxpHYeC0m1RYmaHrFLYiXAYlYdhzbeVAAC4i4LwscL9XM9XKzari_24whKZJrd-MvglbVmuBQ5Da_BKCJg5K8j9g7qxfu68xbLmrOfJf_mTQYxMJNU6VN72_dsSS_d_2VApwaUfqLgVSpZ4lxRSNDKpSJJVh9o_LyLW0JanMr5C4RfhneSD9C6Ft8xI7N75VsF7Y-TnaQsBXb5cLhspVAZyNI3iolu7fuSP7qtQBhN06TQyFmPPZ7aHRfXPHqIiHsy44mt_0qWsjELf3GZa_5TBB-jvHEOedX_lcUDJHWNqkBHLjB0qaI_XFB8ftLqwtV9hlEXHAlgfTUdsqlvgocwZyeYNVMR5IxPctGPAmceVmNgpPq-kXf9auZ01ZLqk7m401iVL9_lGc-mWkPaiErMo_1RxiV8ui-kCZwfWfzSzjoc2BN7ZLfcd1QZuezd5tsEgcoIpo3RTcp-rrIHrmFfB9wJu6ZKVJ3O91fd9gTrn5xi-CFMC2_avjIyfUs-0vLjysMh8vAJfWuoYp0c086UgJuTnrUfK5K3u6ALynVmeUVwLmPmBt-DZz4f34eq6irDeATP3UPN_Jt7QsdeeP-zpIkxbsAIveyeoFvrCDjvXRIK5LcGdWKm-0uiUoukg1t4AevlIrR4r0gKIUmtJSuol1tOp2KbeghGU5pGpAZYzu04iGYJKSga4tBY3tohx5arPv92kR8MNCltfG2NuevApgVYM-shVYrs-b7SYqhJQyUiCo3z1IhLV6kbfw5L6p9aNURBDbW2LzcbYViptXePVQNchGNHTBh-dS5Pgq8LLb1badwh0whvnsvrL7G7_8vsSBAmtEDB4-w-G-hZve3BP9qbbeEGTyavgYwa3R1KUzRwfspAxGH1YXTxBBaEWOYhXpU_vUywpZziuVJrr7cF8A16YBTGqsO57Bg2wfPDW7ZQeOSFdy9k2yDfVpuwm0f9SnQ6l3iS5jlznGTdqR-sEepwZSLZ18idIX91sA0xvaAc6QP3R8XRPNLQuAGbnldK5GcMieoxTU1fmDhuNoIAWpoqXs.G09NmrcDEr999ftBawRBJg'

const STORE = ' BALNEARIO CAMBORIU - SC';
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