// query_pbi.js
const https = require('https');
const fs    = require('fs');

const CLUSTER   = 'wabi-brazil-south-b-primary-redirect.analysis.windows.net';
const REPORT_ID = '0fdd545d-8c7f-4a8a-9723-daf16cac10d6';
const TOKEN     = 'EmbedToken H4sIAAAAAAAEAB3Tx66DVgAE0H95WyLRTIuUBb13jIEdNpd26dUQ5d_zkv0sRmc0f_94-dWNefHz54-68OVnflG9vXGx1XZpaB4ILtZf_60REmONqxKCJh8wWG6IKgog6aiz2eC-VkjWHHq7OwYIJyakq8y8aBbM2pdXBhg2pdnfxXqT_voux8gIriCx6XEIYUCJN_I6Kj95ZDiv0i-KwJmt64Yw1_3DhFV3-6Srx_XHbhg0d61keLNZuDhylxQUjIlheKC5Mu2wf862QZn9Zb1WP1j789Bv8nSusDfdsU7hKGskS4zWZBA9dbihs0GPIeFhVOQoRIXaXCE6bTplnHBsk5Y1QCxfjTx-xaBGEXbmQzA1UC9vUU76pTHDANPtOBVmH-6dnXhalnbZmER3LYM9BPBt-R4j8wOavc1b_eLMtEJ-dM7IPX6FUUN9Vbz83Fc8CD8Yzvi-7rY98QJeuUe1kmk0hzJtiAuj6AJQhaEwbH6K7xN1Cgo9b-aWnWv2Ch9sWMMztUBIJJQvyoeWfZezHm1zJtVlF9Aa1TE3K4NZvisixqjBcrS-YGlsah4TW9cxcvbf5rnS1HRlBM08Rn6fPrwjZm8ijDuT_1SXStcmp5YeSYDMhXjN5XGsJofZgvtpUEoQxJIpK20-UPXni9PbNEcWtGwkteGU8lOmeIWXVlcnsojXSR5pmjxmgZR_dgeBviK9a4gXeoTi1FDs-uxNGEDcuDgBn6VkONzd_15u2yKzVvkyyvDTXVaUnfNR6e31504reDweHxTIiRD0hW-8mavvbJa-I0fDlCAvhZgWpLLh4zrYUJv7yDiKxcCiFY3OnV4GKknHnYNj0sLgzk24CAM-cO63dtV9YUxGVoj5atyHB-EW52shr1PB77HIGupLvSjszORFk8aOWRhUMoupbRVZpYmvdYz9442k4lIvUuuNxQrgSFx-sPNRzCb2mzVppQyBBZtslAbj3TBKYdzsHYo7k-ynmxOZE57pb7nuqqqhReqieqv8iRGLfI_yYuPg7B_1XT3qiB2Mnz9-xOWattEE1--dkyx6ptxX0EwBlnA9VxRFPqrNt8jduMmn3_V5og2za_GpV2eYT9vn5qiv7Y7vJ7BeTZ4xe3x4g9j7jP0x0UPOuwAZWdOCx4WRxQazJvitVVOctvPnghyG-pT4RICnH5LKS98VLvfdj3gCMzvLObokimF548QOMpil4k4F-QIf08UkG11iKpc7Mu-yqGu-Q1nq-gtrdiqzVRQJaqlCzG1HPbRW2S-HaHsdkXCTVK1zf73VwUYW1UOYEa5kUbDqRBStWvUFP5YI7l9EJUW6fMIzkalTBcQ5RpYUnMDWXlzFUXQLd4Zs1oCjEme7AI9CG6ezUThM6XxAjTkeZHYqhRXHPf_XX_8xX1MNFj3-VaYHoNaKwz1EQM3-d7CwgnTO_1NhUw35ti_gvzFUVazKrrS3tCQ8LnZFDUc7jnheFFUU9yNmM9tiHZGREMtnHI8SSIeQp0l6t_69nmwyPI3tPlx7TRv3edp4lxBOcpd2t5Io71B7fezGDLoUAmM5aYfZ1AkNKkkkBtBBcazKAGEeF4U-SqykZLAmVoYhT2IL0ISUda43HNeWDGUBF0_yVrxat1Y9tuFdKYKYjsPzdU-9hMSCPd87T0lPTt_awJ9ArnztOaU_fSEkqXjE-3HTmtvdaBrRLb4q5-9jtP3mbqRZ0upJf1G6K-9r4AsPoqoqR9wqKUTAWFXY5aVvgJZGoNvPndfMTVVTgj5vedx3CoOyXKHU08zxvQuwEQ0-_i_zP_8CVd8NucIGAAA=.eyJjbHVzdGVyVXJsIjoiaHR0cHM6Ly9XQUJJLUJSQVpJTC1TT1VUSC1CLVBSSU1BUlktcmVkaXJlY3QuYW5hbHlzaXMud2luZG93cy5uZXQiLCJleHAiOjE3NzYwMjY5MzQsImFsbG93QWNjZXNzT3ZlclB1YmxpY0ludGVybmV0Ijp0cnVlfQ==';

function request(method, path, body) {
  return new Promise((resolve, reject) => {
    const data = body ? JSON.stringify(body) : null;

    const options = {
      hostname: CLUSTER,
      path,
      method,
      headers: {
        'Authorization': TOKEN,
        'Content-Type': 'application/json',
        'Accept': 'application/json, text/plain, */*',
        'Origin': 'https://dashboardbi.ademicon.com.br',
        'Referer': 'https://dashboardbi.ademicon.com.br/',
        ...(data ? { 'Content-Length': Buffer.byteLength(data) } : {})
      }
    };

    const req = https.request(options, res => {
      let raw = '';
      res.on('data', chunk => raw += chunk);
      res.on('end', () => {
        console.log(`${method} ${path.substring(0, 80)} → ${res.statusCode}`);
        try { resolve({ status: res.statusCode, body: JSON.parse(raw) }); }
        catch(e) { resolve({ status: res.statusCode, body: raw }); }
      });
    });

    req.on('error', reject);
    if (data) req.write(data);
    req.end();
  });
}

async function main() {

  // ── Step 1: get model (tables + columns) ──────────────────────
  console.log('\n--- Fetching model ---');
  const model = await request(
    'GET',
    `/explore/reports/${REPORT_ID}/modelsAndExploration?preferReadOnlySession=true&skipQueryData=true`
  );
  fs.writeFileSync('./model.json', JSON.stringify(model.body, null, 2));
  console.log('Saved model.json');

  // Try to print table names
  try {
    const tables =
      model.body?.models?.[0]?.tables ||
      model.body?.model?.tables ||
      [];
    if (tables.length) {
      console.log('\nTables found:');
      tables.forEach(t => console.log(`  - ${t.name}`));
    } else {
      console.log('Check model.json for table names (search for "tables")');
    }
  } catch(e) {}

  // ── Step 2: paste your captured payload here ───────────────────
  // Once you share the payload from the Network tab,
  // we will fill this in and query the actual data
  console.log('\n--- Ready for data query ---');
  console.log('Share the payload from one of the 2%2F7b2b528 requests');
  console.log('and we will add it here to extract the data.');

}

main().catch(console.error);