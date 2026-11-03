import html, http.cookiejar, re, urllib.request, urllib.error
base='http://localhost:5080'
opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
for path in ['/health/live','/Login','/','/Electricity','/ElectricityTariffs/1','/ElectricityHolidays/1']:
 with opener.open(base+path) as r:
  body=r.read().decode();assert r.status==200
  if path in ['/','/Electricity','/ElectricityTariffs/1','/ElectricityHolidays/1']:assert '/Login' in r.url
  if path=='/Login':
   assert '__RequestVerificationToken' in body and 'type="password"' in body
   assert '/css/site.css?v=' in body and '/css/components.css?v=' in body
   assert '/css/pages/login.css?v=' in body and '/js/site.js?v=' in body
   assert 'style=' not in body.lower() and '<style' not in body.lower()
   assert not re.search(r'<script(?![^>]*\bsrc=)[^>]*>', body, re.I)
   assert not re.search(r'\son[a-z]+\s*=', body, re.I)
  print('PASS',path)
try:opener.open(urllib.request.Request(base+'/Login',data=b'Username=x&Password=y'))
except urllib.error.HTTPError as e:assert e.code==400;print('PASS CSRF rejected')
else:raise AssertionError('CSRF accepted')
with opener.open(base+'/Login') as r: body=r.read().decode()
token=re.search(r'name="__RequestVerificationToken" type="hidden" value="([^"]+)"',body).group(1)
from urllib.parse import urlencode
with opener.open(urllib.request.Request(base+'/Login',data=urlencode({'Username':'migration-test-no-user','Password':'invalid','__RequestVerificationToken':token}).encode())) as r:
 body = html.unescape(r.read().decode())
 assert 'Přihlašovací databáze není dostupná' in body
 assert 'MDB-PLATFORM:' in body and 'Diagnostika MDB' in body and 'Microsoft.ACE.OLEDB.16.0' in body
 assert 'style=' not in body.lower() and 'invalid' not in body
 print('PASS unsupported MDB runtime fails closed with safe development diagnostics')

for path, content_type in [('/css/site.css','text/css'),('/css/components.css','text/css'),('/css/pages/login.css','text/css'),('/js/site.js','javascript'),('/css/pages/tariffs.css','text/css'),('/js/pages/tariffs.js','javascript'),('/css/pages/holidays.css','text/css'),('/js/pages/holidays.js','javascript')]:
 with opener.open(base+path) as r:
  assert r.status==200 and content_type in r.headers['Content-Type'] and r.read()
  print('PASS external asset',path)
