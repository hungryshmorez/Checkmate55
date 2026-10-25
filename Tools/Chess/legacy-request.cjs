const {Chess}=require('./chess.cjs');
function request(q) {
 const c=new Chess(); if(!c.load(q.fen)) throw Error('Invalid FEN');
 const lane=s=>s.charCodeAt(0)<100?0:s.charCodeAt(0)<102?1:2;
 const road=!!q.roadEnabled && c.turn()==='w';
 let moves=c.moves({verbose:true});
 if(road) {
  // Kings retain ordinary escape moves. Other pieces must start in the active lane.
  moves=moves.filter(m=>m.piece==='k'||lane(m.from)===q.lane);
  const pieces=c.board().flat().filter(Boolean);
  for(const p of pieces.filter(p=>p.color==='w'&&lane(p.square)===q.lane)) {
   for(let x=0;x<8;x++)for(let y=0;y<8;y++) {
    const to='abcdefgh'[x]+(y+1),dx=Math.abs(x-(p.square.charCodeAt(0)-97)),dy=Math.abs(y-(+p.square[1]-1));
    const target=c.get(to);if(target&&(target.color==='w'||target.type==='k'))continue;
    let rule='';
    if(p.type==='n'&&q.jumpKnown&&q.speedMph>=70&&((dx===3&&dy===1)||(dx===1&&dy===3)))rule='ROAD JUMP';
    if(p.type==='r'&&q.chargeKnown&&q.speedMph>=90&&dy===0&&dx>1&&lane(to)!==lane(p.square)) {
     let blockers=0,enemy=false;const start=p.square.charCodeAt(0)-97;
     for(let i=Math.min(start,x)+1;i<Math.max(start,x);i++){const b=c.get('abcdefgh'[i]+p.square[1]);if(b){blockers++;if(b.color!=='w')enemy=true;}}
     if(blockers===1&&!enemy)rule='LANE CHARGE';
    }
    // Overclock destabilizes extended moves: only the pulsing destination lane accepts them.
    if(!rule||(q.speedMph>=110&&lane(to)!==q.overclockLane))continue;
    const test=new Chess(c.fen());test.remove(p.square);test.remove(to);test.put({type:p.type,color:p.color},to);
    let fields=test.fen().split(' ');fields[2]='-';fields[3]='-';fields[4]='0';
    test.load(fields.join(' '));if(test.in_check())continue; // Never permit exposing the player's king.
    fields[1]='b';const next=fields.join(' ');test.load(next);
    moves.push({color:'w',from:p.square,to,piece:p.type,captured:target?target.type:undefined,flags:'road',san:rule+(test.in_check()?'+':''),roadFen:next});
   }
  }
 }
 let moved=null;
 if(q.from) {
  const candidate=moves.find(m=>m.from===q.from&&m.to===q.to&&(!m.promotion||m.promotion===(q.promotion||'q')));
  if(candidate){if(candidate.flags==='road'){c.load(candidate.roadFen);moved=candidate;}else moved=c.move({from:q.from,to:q.to,promotion:q.promotion||'q'});}
 }
 return {fen:c.fen(),turn:c.turn(),check:c.in_check(),mate:c.in_checkmate(),draw:c.in_draw(),accepted:!!moved,move:moved,moves:q.from?c.moves({verbose:true}):moves,pieces:c.board().flat().filter(Boolean)};
}

module.exports={request};
