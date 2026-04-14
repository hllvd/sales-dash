// extract_ademicon.js
// Runs both Power BI queries and saves each to a CSV file.
// Run with: node extract_ademicon.js

const https = require('https');
const fs    = require('fs');

// ═══════════════════════════════════════════════════════════════
// CONFIG — paste a fresh token here when it expires (~1hr)
// Get it from: Network tab → any pbidedicated request → Headers → authorization
// ═══════════════════════════════════════════════════════════════
const TOKEN = 'MWCToken eyJhbGciOiJSU0EtT0FFUCIsImVuYyI6IkExMjhDQkMtSFMyNTYiLCJraWQiOiI3NjlGODk2NEQxNzA4Q0VCMDgxQzU4MTBGQ0I2RUJERUZFRjcwRUU4IiwidHlwIjoiSldUIiwiY3R5IjoiSldUIn0.p8zchTpJpYXuhRd6EfheG1JOX-tbF6-cDFoIO8Gn1CforT_1MIfHdQXyi4y_gNQ5iNsdzU2fMiuN-fj809Isq7VNsowCcplCNF7fKAsvBVPJZxds61tw9uVuEkAuSeZXXkab7L8CzemASKuzpv8UiblGnzLbCybuIRN4m0QUj2nGz4OtQ1_WS8E_O52X4bTu8ptjDhjB4CRLnssK7yWKLUUG0wXSDNfC09T4VpgIyL9MS3aWgMcf0ZV7CNPlqts1_KI55a0qgQIqyQHP9PWxzdlwMqYS_jN5wKY-Cf56JUqPByly6GDfDRi2gIVhGJnf4nc57U4hwLIIHvheQoJukw.ndl24eeKedkYD_3iHu4mqw.bNQKa2z2uZ0_vysDSrtIj_rm8kKa7IqqJoUXbj1FR6Om4eGKTlx5MVQaAmPUBtJqEqr0MI4UJ5Tyq3jVBqhTtgXBoxfN-iIH9QW33aS9yFDOxQar_nHnRQ3cFcrY9l4KLHLWpGmG3BPkqSwoKCheAJqCHSx66WtIiM-C46F4XkiAWm1NsuKU3B8jCnwsPrrfVUVca_tDBDqavgb-A452t26mpGuB-hB03-8yr4xxjhbD-QsmJmCH3buKvOfS8aRy3SflxTBKYwYQ1Xn1gkjSD--owl6YO_6ICcHHhcTGAsR-j41lqaBl2N3VTAC_x9tZYXs4LeeJjUJqoQ_NNW1Urvh7kqCqILvUl1v1P3qF01dD09nCITDvfMCFvRTAheDcWt3tuaJvIvXi6Gc_fTTk8hipLYpKheEOubqC59moQS1Z-Yh2nDmLLJk0KStHXtcYelDo4fzbFHjUy-tW4zn1b45N2YVyuI_hIMsPVcrn0lwxIoc8i1q0IOrIqVV7WSInqViJ639VyhkJwpOjlB5jDOQr_ECebCY-F5nisy7CLB5SPxyzbCO9_lKyK4JvnHazAhfpVHeidojoU41pFsNpDi4kGcX_toh5dGtcvrC8HXx85JplFJvC27Uki7b-eDXVTYu65Mh4vQyPJhivIqRKG0prSBTnhx5BKtP-qZnaPVKxpbEjXUqCHsu_ObXYHqqUgtlwoLWe2OX6frg1aEq8ihChtvGodpljOzccgX7YdHfV29uvcaoTMnjBuWlH-dXxLEo9Go-cHgq0OZeJG0riZCseVLCvvgdNpbmoAY2eQlijXWtvMzJq4R_QrNubj4Trht4PpqoXUesRsA8meyyTtJIBXAR4IYzuZj84D0BlC9awyjRwtluXtahJbxjE5VG5hXpxDZkXjkuSNSbAnIhKE2rx8ppTLTiVIiSR7RU5mPjkfPgahRWKRczLk5hf3_C1ffFL-8ui40TWBEP67yUOUyM4H5GBtsVgM-_u5tCggsIrbHPLAIzkAd8CYBEvxRRJqAfSuBHSoHEP21pmYUIKjVrEFF1CX5g5-5766gaekD0Y_cSiR0t6cIilC-FY4yVTUA2uQ2G5np-Ex5pzzUhXQqf-J2r-OA21AvBi5wz8hk73Ga7izygrhWjy2LJWU3tv2APAeFk-eETw0Da42APeuUmxM3rfy6ymlTK8iiExtcC3iCcgaZ2YF0MdgQZU0ybYedg1AhFsGFV3rDKYBVAFIQc_XwOFpYgy-tdM6oU7V_D9XKPaCxmZY567-XCBMJca40LeDtr7TXoCHQgNzEP13NVAW2yHvlfXYNkNu8IVYggR4_gs3YIxGNuJwzxV6OEtwW50r0NckShOYWSyxCGPFPoFhyvcUXqp0bLuMuHNpu7_GM0F-57Ipykgr3oO7_p2eKuzoaDpqzhllEbtBI4Rv35o9UfvykCtZuhL6ePFdNBs7bMl8ki3-qZEBcJHK9grFiULgFxe81qg4W5ek_KXSoiyZtvEjNwsvzQf4HptjUwN-GIh00ol4HmSzLD928spta47ko4W9i0eWGkKQzVabMZL3rE87N9lFR9HO578KETquFo74w8zbw0fAZTn_pyfVa6SotyebAULYcHCWRJ9QdCud-sF5CVM9xuyFDr3GI2spU681ZjEYRS-KbnS62PgE8lJuTweM9uS8TCmcag3-FSWEfVdIsN7Lh_sYx9kYovW0yorvCugDpP3_6YSrXvwynaNGrSDcU-2CRMg1FrTWQ2xJ3rCC_joAZR9lgZQ9CbspnMllnnf71LHO_vs01OglQFsBh_IIajquhQgcqT2qfjghjvvrXnEtBHzoOm_xDbfa1pMmwd9YUw44LyDG2pYAqS8h1kd_3PUFd5gH_TY3QdR1POlT6LV-sWIh1vtb37z6_4rXlzGblugKLxr63W5wPc09438CCf7AW1l-1ReOwN7Y7kzPQi9PRXJKHKm7QTFbMkpUBgb-9nTfRuwc2XdTOiUHjip88kcVyts1BkEyuIXzlO0_owiyTS0JbjnYDKtP5V_pke93qmX9WO8__K3HoHnTdmac633Naf0ol2g8uGhDEXWN8nEY4ojVEETWbRyNHOYN3UAIQYVQyGuCMKMlZQoofFtRrk-IjG6loL4w2UwON9gu3udrqe-izUQXYJog3kM2tDoeQFvLdo2hY4bLUGigojJRGJMG1edSc72msvGcrQTzECOxfAyr6YcMFwQOGij2alJW2Ka1-aUlODPKFmu5h8yeCjKyJYA54QPz0Jt97Y1ldBRsZp4jkujDLYcXvRlQwyvN6ydGyyBsGnbje7Ez08EpN3DjannrFOt_QZkwqZi5ONElLBZWZ2-ginfOApkeHpZJ6rj6OUWv00nJrJDy9PtZjNozd445SHXWnLLgeK6hDxBHRUeRfeXdC-r6002egQJJli8mynZnXh54WN34SsYH2YLbnqP3iFwiUt3gkc_4fhZTE5PIQecIqwdsL2za1uJ1bwU38icHJcLQ7usYJz81N4Bu6Sg7CXKm8kLuUVwhwvFN1bgAnNwXiJpX8W_e9_4YD5qyIANH54W4YaSrqOGBDsYTNPn2HcwG-o_l41c7z0AR8qP2Gu7aTctbAyWkSHv8nOyEISFxQjPM1k5SSHMZ6gZpP5e5oGNc_5ZY1HfF7cHZr7RLTnVugp9TFBhH1vMrHvNrtpEh_e4Udn8KEwfEuD8ooo5pboJJtCa9zBSz8T9nxRUODh-RDYErUcDQ_IMGFPYhalpX1wlZX1iXCkv5VMYLSBQC8s9YK_L_OBEV-GT_eB3g9tbYSx_c-_PhI50NLaysUi6UadDi_kWbx3PNpaaaKHpDs5CIVupXDvdIr_Z7z76bjXa8aatWOtdrV5zzdg9ALGFx5d0BkSrVtBR3w8RHr4Y5nuD3p_2v_c-RDFK7kPEcGYLyOYj6VKlk2VKd5haNHwK4AK_3SZJw5idRJo8rSnNJPiuDofDiUCq6pC1npRhqCVNg5L9ZV-jWFXkReoUspdnfvdZNXwccwmLdewKLAsZdjxDcR1GfLW2D0NF1q6zVUrFqADvGvMJtNMPuCRmD1_kHTg5zxrwn7tf7NldQ2p6z9wKM8QTmn_QdCxzOVFX9MhnFxcXGDxdOoW3m9zlxcSnF_YuTRhpki5VFJ3GmFaENWBRh8Quh2P3bHFoGyAq4G9nCe4OlM0TQEMG8xM4Ayx3ITxY0axeIp0fL8FPy9tjqDIKCkYhjU1Nyrorfu_CEEqRuW90nanJLO9VAV1AVWQ1oMrdzPJC460N714Qehf55GUtVtV-E0ud8lYYAhRyVpd4rfg7JYuN7lnX2MuLzSBXrpg96YuRZa2xxJSTIQ8qUz53IU-OtPN0QhifOydteG0Qlge8_rj9aPcMdxqaEsCHAUQXetEVA86-ktPk7bGdNC6CfoJtxWaIu__Jp89zQOY1ArtBUKCD8nXJmOPWBLOixHuCPCpMnZuXtiL0c2xXzwb6vWOnAAY2R53wtSl1op6XPTmRUvjbWML3n7T9qwmMqBYkSh6KzEXw3dBFx9omYJTcWO8QXghqs0cwEGQpEasbWm563pgQjkBTkQ4V1oGkhdndi1lORCdEjtLcMy-JykwDkWo42bO8GFWEUlyhsjXfLqEFqSeZORCQWR708mr6GoXKeMIXuPNBoHDfapBnebpprbRun9g-nSkRZtcwMJK9xrs8aBmi74ftDBDvq7rU3HFt9Iu4DJS2jSknhSSMq9xaJKtpW8N0sBspniM6C41EwWxE4MbnwkOt3k7Fbgf6TtZ3RjXXTrGoPZJAgk8BdJoxFczrmptCIN-jBcl1Ap6hZUaLgA2Cb3PFEDLMdRsW84wCzY3szYIiHl9L3s4QLX8-3JYr7DvkMc7-ZAlDDSRI8GU1BCLdF7_tAw7sQ8hj3fQYOZnKA6NmF_YQlzkiHJTGGXeRWWNlIGwNvdXZFF9cSwvs7DIgaOirKHWuIOaxHzMpRTEZCrFAe3edTt3F7glTLdWuf5TLRs8hZhJlw5FSCawB6kfmajJc_us-fgc1yjQjnk03eC546l1_NwuxnKICusps8PZTV2T60y9xNyuF-xo9indEMt2Qpzzkht5N1oXCtpoA2Do3liL3mRX4CfFR9NRS4Nc2Uckz7qao6epGR2MlZCTZDrSWVbkW40zBZVXZoL5xjLRJd0pnv_DA82X9k83YK-xSuV-Pq9HwjG33LaBW6cEXGhiHrtVSB5RKaZOi6Fru1EkBkRPheIfjleiEeeG0I-brpW7uN_gshQyOKHk4BoOGiSHujzDYsCVe8VFGVdgqfZ5cw7HApz1CDNU3Br_jMDyHoNTiKunVASI0n-4jHMkMbXl0Xm02OTh1VFxpLV7XRlVkWXwJ3V6KPSRsT0m0xrXgCgpU8ZhrVGjK3lZpsVgkV3fU8Bq-oz0IHYgC0IhLVc8OzwbCNUcUM7pyQWuPDrkramEtjeqZLbRgrcvoBKGXoplY52JHMLr0uiYBbhdCAcvOP9E6IzkN600p_AeiZajupJRClCrrD77ragrwcCuKXIctSR_wy8DYECT83yr4GuFUkB0OzgVmjoabBCpIr1ivb9WhxhVFxviJG3hphfq_tTkLtt5OnORGiimAwc_UcPfCXmfpdjy5Cisn7_DTGQ_SGQSF009b04vVMuACO2GItL-_OI_5QRh7Z6hgokYna2mjzsgaTL-Pq56eEbOPe_6WggGsycUTUnojujumnp8TbTD9JoJYLKiT-93ojrlHvGvTiz06rF9B_kzwfhdAHtNabx3ooIs65wZltMN7IKF0306f_Abl2dYeL_Edkp28IeWwg2iKkEZx_DMAoGLet-An9isYDdNw_NFSZUmstGJpG-E4VCnm3U3EJ4Bi9cw6TGWDzXfwSU7hlo-bJ6l8eQXrKS8935_OgBIkPFf4mmb98A7cdG9F5QvO4Wp3Zz2Opgf-uGO1wHMu4ZXbEBBe7riL-6Ggcdw9dQ9EQS_TbFhww6yu6cwYtwjxnPrcBJaf8mDMzpxZoVsfBTXju-N8vjtLFriLuQbqSendxlrE4BMVgO4EZUoj7aL9jFUGCt8RIBw_2Z-WpIipB8XAhX5hnsthqRk10aJpMj8n-kHgaVDUW-crf68kfcEkhvnOm0Sxz-0Jjz9d8J-dFYS1vUKTLZv3LFKnkZsgAtFzNGMPD8NWfrUZKmK5tK7IJbJa9QkI-JbFwkvHMFP00H_4VwSFW7Pch4H1y3UBnhPTscX3v3G5BJk8C2dhhoAyOzs8uAJ3u3yufPcio9B6diCgH5VGwP164lH1i1Y5lXaIY12DWwGSbHspz8YitNJbDH0nkPjluiIfVSi46nrlHb7P4nYSFnliptQ48ALphSPalZaM27gqR2nrXEg2GB5C5nu4loSHaa4Ol7CcAfjGuCCy2yl32CEIWEX66mnQDMBtNr4M9niLCIQIBlLslRZqpvb2Z6aJsYRXlyn0wZJjCDxInaW0B3lA3i_L12RrVzgjNCH-ei4IDU-h-CLCabXHvFzpA_Q2TqznEb5v_yITuNY_DIricLKyVv1hNsrJ-v90R4yokxB-t_XbjqYidx_My8LquFiVnqsC8Vg9BbKrEHZ_MR29BmeQxqhuzm4wAcxJXtOsIxBKHw4p7VjeOL5dKqRm70vvM6TwVjX-tsY4KtDcl2VnUHs3-QbX2hIFL4z6vJCyL7dXXf1irCu058gKwA03Yd2YEb1hk2P22NSW-WRPHvBxp36XUw2bTvp5zoFFpyBW9YeiOKL5rc--T_5KM4wdZogPJuQZTEKuR0Gtov2YPDXpCX-ZrS-33S6FpoBvAksgz8Pa-24uXhmGse7bwOZzF6UF72PTJBpN8vPmDjEO_Op4mKWuV0rxdGu58ptUX1eHNk2evvXk4l5MiNBYM1FdIug9JqwXyPOttUT7StxLvV2dSluYocr_9bwd0WX8vx9z3kSHvpOsFdhrHyXjTJuqUtDMrsNr3dr15TkqjpVx7h3LjfXleGNecv7tdMZOCs6tVdaB-9AKp33iD187Yx66drkmy6_AcCRqkPZ7m0X9uPX9wOa2CPBATIrIIsGPYNT_iNEHwImxYZGWHqOXIub3u8jxRESkVgNmpzuh_ByaJtUYzI3Bkhm3wpzrlp1i0Go.1ls3cH_YFZ_fL8l8NWjRFQ';

const ENDPOINT = '7a8110990e16404daec259c355434bc6.pbidedicated.windows.net';
const PATH     = '/webapi/capacities/7A811099-0E16-404D-AEC2-59C355434BC6/workloads/QES/QueryExecutionService/automatic/public/query';

// ═══════════════════════════════════════════════════════════════
// QUERY 1 — Summary (indexes + dicts) → output_summary.csv
// Columns: Produção Oficial, Retenção, CNPJ, Região, Consultor, Unidade Original
// ═══════════════════════════════════════════════════════════════
const PAYLOAD_QUERY1 = {"version":"1.0.0","queries":[{"Query":{"Commands":[{"SemanticQueryDataShapeCommand":{"Query":{"Version":2,"From":[{"Name":"m","Entity":"1_Medidas","Type":0},{"Name":"t","Entity":"tbl_cotas","Type":0},{"Name":"r","Entity":"Regiões","Type":0},{"Name":"p","Entity":"Parâmetro_Senhas","Type":0},{"Name":"a","Entity":"acessos","Type":0}],"Select":[{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Produção Oficial"},"Name":"Medidas.Produção Oficial","NativeReferenceName":"Produção Oficial"},{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Retenção"},"Name":"Medidas.Retenção","NativeReferenceName":"Retenção"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"id_consultor"},"Name":"tbl_cotas.id_consultor","NativeReferenceName":"CNPJ"},{"Column":{"Expression":{"SourceRef":{"Source":"r"}},"Property":"regiao"},"Name":"Regiões.regiao","NativeReferenceName":"Região"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"Consultor_Matricula"},"Name":"tbl_cotas.Consultor_Matricula","NativeReferenceName":"Consultor"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"},"Name":"tbl_cotas.nm_unidade_bi_original","NativeReferenceName":"Unidade Original"}],"Where":[{"Condition":{"Comparison":{"ComparisonKind":1,"Left":{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Produção Oficial"}},"Right":{"Literal":{"Value":"0L"}}}},"Target":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"id_consultor"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"Consultor_Matricula"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"}},{"Column":{"Expression":{"SourceRef":{"Source":"r"}},"Property":"regiao"}}]},{"Condition":{"Comparison":{"ComparisonKind":1,"Left":{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Filtro de Cotas"}},"Right":{"Literal":{"Value":"0L"}}}},"Target":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"id_consultor"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"Consultor_Matricula"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"}},{"Column":{"Expression":{"SourceRef":{"Source":"r"}},"Property":"regiao"}}]},{"Condition":{"In":{"Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"Grupo Ativo"}}],"Values":[[{"Literal":{"Value":"'Sim'"}}]]}}},{"Condition":{"In":{"Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"}}],"Values":[[{"Literal":{"Value":"'BALNEARIO CAMBORIU - SC'"}}]]}}},{"Condition":{"And":{"Left":{"Comparison":{"ComparisonKind":2,"Left":{"Column":{"Expression":{"SourceRef":{"Source":"p"}},"Property":"Parâmetro_Senhas"}},"Right":{"Literal":{"Value":"929009D"}}}},"Right":{"Comparison":{"ComparisonKind":4,"Left":{"Column":{"Expression":{"SourceRef":{"Source":"p"}},"Property":"Parâmetro_Senhas"}},"Right":{"Literal":{"Value":"929009D"}}}}}}},{"Condition":{"In":{"Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"a"}},"Property":"matricula"}}],"Values":[[{"Literal":{"Value":"'011177'"}}]]}}}],"OrderBy":[{"Direction":2,"Expression":{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Produção Oficial"}}}]},"Binding":{"Primary":{"Groupings":[{"Projections":[0,1,2,3,4,5],"Subtotal":1}]},"DataReduction":{"DataVolume":3,"Primary":{"Window":{"Count":500}}},"Version":1},"ExecutionMetricsKind":1}}]},"QueryId":"","ApplicationContext":{"DatasetId":"ecfe45f3-4e9f-4a6e-9d56-91020972365d","Sources":[{"ReportId":"0fdd545d-8c7f-4a8a-9723-daf16cac10d6","VisualId":"081f7f5c60a98980243b"}]}}],"cancelQueries":[],"modelId":5258155,"userPreferredLocale":"en-US","allowLongRunningQueries":true};

// ═══════════════════════════════════════════════════════════════
// QUERY 2 — Detail (raw values) → output_detail.csv
// Columns: Versao, Consultor, Matricula, PV, Crédito Venda, Dt Produção,
//          Dt Venda, Categoria, Cód. PV, Dt Cancelamento, Unidade Atual,
//          Obs Cota, Produção Analitica, id_bi, Cota, Prazo Cota,
//          Prazo Grupo, Tem Pagamento?, Dt Contemplacao, Unidade Original,
//          Qtd Parcelas Atraso, Plano Venda, Situação Cobrança
// ═══════════════════════════════════════════════════════════════
const PAYLOAD_QUERY2 = {"version":"1.0.0","queries":[{"Query":{"Commands":[{"SemanticQueryDataShapeCommand":{"Query":{"Version":2,"From":[{"Name":"2","Entity":"2_Medidas_Tabela","Type":0},{"Name":"t","Entity":"tbl_cotas","Type":0},{"Name":"m","Entity":"1_Medidas","Type":0},{"Name":"p","Entity":"Parâmetro_Senhas","Type":0},{"Name":"a","Entity":"acessos","Type":0}],"Select":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"versao"},"Name":"Sum(tbl_cotas.versao)","NativeReferenceName":"Versao"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_consultor"},"Name":"tbl_cotas.nm_consultor","NativeReferenceName":"Consultor"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"id_matricula"},"Name":"tbl_cotas.id_matricula","NativeReferenceName":"Matricula"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_pv"},"Name":"tbl_cotas.nm_pv","NativeReferenceName":"PV"},{"Aggregation":{"Expression":{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"vl_credito_venda"}},"Function":0},"Name":"Sum(tbl_cotas.vl_credito_venda)","NativeReferenceName":"Crédito Venda"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_producao"},"Name":"tbl_cotas.dt_producao","NativeReferenceName":"Dt Produção"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_venda"},"Name":"tbl_cotas.dt_venda","NativeReferenceName":"Dt Venda"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"categoria_consultor"},"Name":"tbl_cotas.categoria_consultor","NativeReferenceName":"Categoria"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"cd_ponto_venda"},"Name":"tbl_cotas.cd_ponto_venda","NativeReferenceName":"Cód. PV"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_cancelamento"},"Name":"tbl_cotas.dt_cancelamento","NativeReferenceName":"Dt Cancelamento"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_atual"},"Name":"tbl_cotas.nm_unidade_bi_atual","NativeReferenceName":"Unidade Atual"},{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Obs Cota"},"Name":"1_Medidas.Obs Restrições Cota","NativeReferenceName":"Obs Cota"},{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Produção Analitica"},"Name":"1_Medidas.Produção Analitica","NativeReferenceName":"Produção Analitica"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"rn"},"Name":"Sum(tbl_cotas.rn)","NativeReferenceName":"id_bi"},{"Measure":{"Expression":{"SourceRef":{"Source":"2"}},"Property":"Cota"},"Name":"2_Medidas_Tabela.id_cota","NativeReferenceName":"Cota"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"pz_cota"},"Name":"Sum(tbl_cotas.pz_cota)","NativeReferenceName":"Prazo Cota"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"pz_comercializacao"},"Name":"Sum(tbl_cotas.pz_comercializacao)","NativeReferenceName":"Prazo Grupo"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"tem_pagamento"},"Name":"tbl_cotas.tem_pagamento","NativeReferenceName":"Tem Pagamento?"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_contemplacao"},"Name":"tbl_cotas.dt_contemplacao","NativeReferenceName":"Dt Contemplacao"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"},"Name":"tbl_cotas.nm_unidade_bi_original","NativeReferenceName":"Unidade Original"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"qtd_pc_atraso"},"Name":"Sum(tbl_cotas.qtd_pc_atraso)","NativeReferenceName":"Qtd Parcelas Atraso"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_plano_venda"},"Name":"tbl_cotas.nm_plano_venda","NativeReferenceName":"Plano Venda"},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_situacao_cobranca"},"Name":"tbl_cotas.nm_situacao_cobranca","NativeReferenceName":"Situação Cobrança"}],"Where":[{"Condition":{"Comparison":{"ComparisonKind":0,"Left":{"Measure":{"Expression":{"SourceRef":{"Source":"m"}},"Property":"Filtro de Cotas"}},"Right":{"Literal":{"Value":"1L"}}}},"Target":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"versao"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_venda"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_producao"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_cancelamento"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_contemplacao"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"id_matricula"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"categoria_consultor"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_consultor"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"cd_ponto_venda"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_pv"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_atual"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"tem_pagamento"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"pz_cota"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"pz_comercializacao"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"qtd_pc_atraso"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_situacao_cobranca"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_plano_venda"}},{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"rn"}}]},{"Condition":{"In":{"Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"nm_unidade_bi_original"}}],"Values":[[{"Literal":{"Value":"'BALNEARIO CAMBORIU - SC'"}}]]}}},{"Condition":{"And":{"Left":{"Comparison":{"ComparisonKind":2,"Left":{"Column":{"Expression":{"SourceRef":{"Source":"p"}},"Property":"Parâmetro_Senhas"}},"Right":{"Literal":{"Value":"929009D"}}}},"Right":{"Comparison":{"ComparisonKind":4,"Left":{"Column":{"Expression":{"SourceRef":{"Source":"p"}},"Property":"Parâmetro_Senhas"}},"Right":{"Literal":{"Value":"929009D"}}}}}}},{"Condition":{"In":{"Expressions":[{"Column":{"Expression":{"SourceRef":{"Source":"a"}},"Property":"matricula"}}],"Values":[[{"Literal":{"Value":"'011177'"}}]]}}}],"OrderBy":[{"Direction":2,"Expression":{"Column":{"Expression":{"SourceRef":{"Source":"t"}},"Property":"dt_producao"}}}]},"Binding":{"Primary":{"Groupings":[{"Projections":[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22],"Subtotal":1}]},"DataReduction":{"DataVolume":3,"Primary":{"Window":{"Count":500}}},"Version":1},"ExecutionMetricsKind":1}}]},"QueryId":"","ApplicationContext":{"DatasetId":"ecfe45f3-4e9f-4a6e-9d56-91020972365d","Sources":[{"ReportId":"0fdd545d-8c7f-4a8a-9723-daf16cac10d6","VisualId":"cc96a767b12339a67c49"}]}}],"cancelQueries":[],"modelId":5258155,"userPreferredLocale":"en-US","allowLongRunningQueries":true};

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
// DSR PARSER — handles Power BI's compressed format
//
// The DSR format works like this:
//   ValueDicts: lookup tables (D0, D1, D2...) — actual string/number values
//   DM0:        subtotal row — we skip it
//   DM1:        data rows — each row has:
//     C: array of values (new values only)
//     R: bitmask — if bit N is set, position N repeats from the previous row
//        instead of being present in C
//
// The `descriptor` tells us which column maps to which dict:
//   Kind=1 → column (lookup index into a dict)
//   Kind=2 → measure (raw number, no dict lookup)
// ═══════════════════════════════════════════════════════════════
function parseDSR(data, queryName) {
  // Save raw for debugging
  fs.writeFileSync(`./raw_${queryName}.json`, JSON.stringify(data, null, 2));

  const result = data?.results?.[0]?.result?.data;
  if (!result) throw new Error(`[${queryName}] No result.data found`);

  const descriptor = result.descriptor;
  const ds         = result.dsr?.DS?.[0];
  if (!ds) throw new Error(`[${queryName}] No DS[0] found`);

  // ── Build column map from descriptor ─────────────────────────
  // Maps value alias (M0, G0, G1...) → { name, dictKey, isDict }
  const selectItems = descriptor?.Select || [];
  const colMap = {};
  selectItems.forEach(item => {
    const alias = item.Value;
    colMap[alias] = {
      name:    item.NativeReferenceName || item.Name,
      dictKey: item.DN || null,   // e.g. "D0", "D1" — present only for columns with dicts
      isDict:  item.Kind === 1,   // Kind=1 = column (dict lookup), Kind=2 = measure
    };
  });

  // ── Load value dictionaries ───────────────────────────────────
  const dicts = ds.ValueDicts || {};

  // ── Find DM1 schema + rows ────────────────────────────────────
  const ph = ds.PH || [];
  let schemaRow = null;
  let dataRows  = [];

  for (const group of ph) {
    if (group.DM1) {
      for (const entry of group.DM1) {
        if (entry.S) {
          schemaRow = entry; // schema defines column order for this group
        } else {
          dataRows.push(entry);
        }
      }
    }
  }

  if (!schemaRow) throw new Error(`[${queryName}] No schema row (S) found in DM1`);

  // The schema S array tells us the ORDER of columns in C values
  // Each element: { N: alias, T: type, DN: dictKey }
  const schema = schemaRow.S; // e.g. [{N:"G0",T:1,DN:"D0"}, {N:"G1",...}, {N:"M0",...}]

  console.log(`[${queryName}] Columns (${schema.length}):`, schema.map(s => s.N).join(', '));
  console.log(`[${queryName}] Data rows in DM1: ${dataRows.length}`);
  console.log(`[${queryName}] Dicts: ${Object.keys(dicts).join(', ')}`);

  // ── Parse rows using carry-forward (R bitmask) ────────────────
  const numCols = schema.length;
  const prev    = new Array(numCols).fill(null); // carry-forward state
  const rows    = [];

  for (const entry of dataRows) {
    const C = entry.C || [];
    const R = entry.R || 0;

    // Resolve each column position
    const resolved = [...prev];
    let ci = 0;

    for (let pos = 0; pos < numCols; pos++) {
      const isRepeated = (R >> pos) & 1;
      if (!isRepeated) {
        resolved[pos] = C[ci] !== undefined ? C[ci] : null;
        ci++;
      }
      // If repeated: keep prev value (already in resolved)
    }

    // Update carry-forward
    for (let i = 0; i < numCols; i++) prev[i] = resolved[i];

    // Build output row: resolve dict indices to actual values
    const row = {};
    schema.forEach((s, i) => {
      const alias   = s.N;
      const dictKey = s.DN;           // e.g. "D0"
      const rawVal  = resolved[i];

      let finalVal;
      if (dictKey && dicts[dictKey] !== undefined) {
        // It's a dict index — look up the actual value
        finalVal = dicts[dictKey][rawVal] ?? rawVal;
      } else {
        // It's a measure or raw value
        finalVal = rawVal;
      }

      // Use friendly column name from descriptor
      const friendlyName = colMap[alias]?.name || alias;
      row[friendlyName] = finalVal;
    });

    rows.push(row);
  }

  return rows;
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
  console.log('═══════════════════════════════════════');
  console.log('  Ademicon Power BI Extractor');
  console.log('═══════════════════════════════════════\n');

  // ── Query 1: Summary ─────────────────────────────────────────
  console.log('▶ Running Query 1 (Summary)...');
  const raw1  = await post(PAYLOAD_QUERY1);
  const rows1 = parseDSR(raw1, 'query1_summary');
  console.log(`  Rows: ${rows1.length}`);
  if (rows1.length > 0) {
    fs.writeFileSync('./output_summary.csv', toCsv(rows1), 'utf8');
    console.log(`  ✓ Saved → output_summary.csv`);
    console.log(`  Sample:`, rows1[0]);
  } else {
    console.log('  ⚠ No rows. Check raw_query1_summary.json');
  }

  console.log('');

  // Small pause between requests
  await new Promise(r => setTimeout(r, 1000));

  // ── Query 2: Detail ──────────────────────────────────────────
  console.log('▶ Running Query 2 (Detail)...');
  const raw2  = await post(PAYLOAD_QUERY2);
  const rows2 = parseDSR(raw2, 'query2_detail');
  console.log(`  Rows: ${rows2.length}`);
  if (rows2.length > 0) {
    fs.writeFileSync('./output_detail.csv', toCsv(rows2), 'utf8');
    console.log(`  ✓ Saved → output_detail.csv`);
    console.log(`  Sample:`, rows2[0]);
  } else {
    console.log('  ⚠ No rows. Check raw_query2_detail.json');
  }

  console.log('\n═══════════════════════════════════════');
  console.log('  Done!');
  console.log('  Files created:');
  if (rows1.length) console.log('    • output_summary.csv');
  if (rows2.length) console.log('    • output_detail.csv');
  console.log('═══════════════════════════════════════');
}

main().catch(e => { console.error('\n✗ Failed:', e.message); process.exit(1); });