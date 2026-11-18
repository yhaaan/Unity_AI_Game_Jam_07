엔딩 크레딧 카툰 애니메이션
==========================

구성: 크레딧 카드 4장 (1920×1080). 왼쪽에 애니메이션 컷 2개, 오른쪽 35%는 이름 칸.
  카드1: 04 먼지구름 · 05 이건 누구 발?      카드2: 06 왼발부터 · 07 건초 다이브(FWUMP!)
  카드3: 08 오리 발자국 · 09 건초 위장 도주   카드4: 10 집으로 · 11 잘 자 (새근새근 + 꿈속 발차기)
  (01 탈출 · 02 눈치 · 03 작당 컷은 뺐습니다. 다시 넣으려면 원본에서 build_cuts.py로 변환 후 layout.json에 추가)
자동 넘김(카드마다 컷 인트로 + 5.5초), 탭/클릭/Space/Enter/→ 로 바로 다음.

이름 바꾸기
  web/credits.json (Unity는 Unity/EndingCredits/Resources/EndingCredits/credits.json)
  title = 큰 제목, lines = [{role 역할, name 이름}]. 지금은 "Name" 자리표시자입니다.

웹 미리보기
  web 폴더에서  python3 -m http.server 8766  →  http://localhost:8766/index.html

Unity
  1) Unity/EndingCredits 폴더를 Assets 안(예: Assets/99. Externals/EndingCredits)에 넣기.
     프롤로그(ChimeraGroove.Prologue)와 TextMeshPro가 있어야 합니다(이미 프로젝트에 있음).
  2) 빈 GameObject에 EndingCreditsPlayer 붙이고 Play. 카메라는 Orthographic.
  3) Finished 이벤트(마지막 카드 다음), CardShown(int) 이벤트, Next()/Previous()/GoTo(i)/Replay().
  * 기본 TMP 폰트(Engcomica)에는 한글이 없어서, 한글 이름을 쓰려면 font 칸에 한글 TMP 폰트를 넣으세요.

애니메이션 고치기
  _tools/gen_anims.py 수정 → python3 -I _tools/gen_anims.py web  (anim.json 다시 생성)
  Unity 쪽은 Resources/EndingCredits/<컷>/anim.json 으로 복사.
