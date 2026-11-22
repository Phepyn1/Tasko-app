const CATEGORIES = [
  {
    name: 'Encanador',
    sub: 'Vazamentos e reparos',
    icon: '🔧',
    grad: 'linear-gradient(135deg, #0D9488, #0F766E)',
    img: 'https://images.unsplash.com/photo-1585704032915-c3400ca199e7?auto=format&fit=crop&w=600&q=80'
  },
  {
    name: 'Eletricista',
    sub: 'Fiação e tomadas',
    icon: '⚡',
    grad: 'linear-gradient(135deg, #D97706, #B45309)',
    img: 'https://images.unsplash.com/photo-1621905251189-08b45d6a269e?auto=format&fit=crop&w=600&q=80'
  },
  {
    name: 'Mestre de Obras',
    sub: 'Reformas e alvenaria',
    icon: '🧱',
    grad: 'linear-gradient(135deg, #064E3B, #022C22)',
    img: 'https://images.unsplash.com/photo-1504307651254-35680f356dfd?auto=format&fit=crop&w=600&q=80'
  },
  {
    name: 'Carpinteiro',
    sub: 'Portas e madeira',
    icon: '🪚',
    grad: 'linear-gradient(135deg, #78350F, #451A03)',
    img: 'https://images.unsplash.com/photo-1533090161767-e6ffed986c88?auto=format&fit=crop&w=600&q=80'
  },
  {
    name: 'Lavador de Sofá',
    sub: 'Higienização profunda e impermeabilização',
    icon: '🛋️',
    grad: 'linear-gradient(135deg, #059669, #064E3B)',
    img: 'https://images.unsplash.com/photo-1555041469-a586c61ea9bc?auto=format&fit=crop&w=600&q=80',
    full: true
  }
];

let currentUser = JSON.parse(localStorage.getItem('tasko_user') || 'null');
let selectedRole = 'CLIENTE';
let activeCategory = 'Todos';
let currentServices = [];
let selectedService = null;
let ratingOrderId = null;

function esc(str) {
  return String(str || '').replace(/[&<>"']/g, m => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  }[m]));
}

function getCatMeta(name) {
  return CATEGORIES.find(c => c.name === name) || CATEGORIES[0];
}

function showToast(msg) {
  const el = document.getElementById('toast');
  el.textContent = msg;
  el.classList.add('show');
  setTimeout(() => el.classList.remove('show'), 2800);
}

// ==========================================
// AUTENTICAÇÃO
// ==========================================
function switchAuthTab(mode) {
  document.getElementById('tab-login').classList.toggle('active', mode === 'login');
  document.getElementById('tab-register').classList.toggle('active', mode === 'register');
  document.getElementById('form-login').style.display = mode === 'login' ? 'block' : 'none';
  document.getElementById('form-register').style.display = mode === 'register' ? 'block' : 'none';
}

function selectRole(el) {
  document.querySelectorAll('.role-opt').forEach(r => r.classList.remove('selected'));
  el.classList.add('selected');
  selectedRole = el.dataset.role;
}

async function handleLogin(e) {
  e.preventDefault();
  const email = document.getElementById('login-email').value;
  const senha = document.getElementById('login-senha').value;

  const res = await fetch('/api/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, senha })
  });
  const data = await res.json();
  if (!res.ok) return showToast(data.erro || 'Erro ao entrar.');

  loginSuccess(data.usuario);
}

async function handleRegister(e) {
  e.preventDefault();
  const payload = {
    nome: document.getElementById('reg-nome').value,
    email: document.getElementById('reg-email').value,
    telefone: document.getElementById('reg-telefone').value,
    regiao: document.getElementById('reg-regiao').value,
    senha: document.getElementById('reg-senha').value,
    tipo_perfil: selectedRole
  };

  const res = await fetch('/api/register', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  const data = await res.json();
  if (!res.ok) return showToast(data.erro || 'Erro ao cadastrar.');

  showToast('Conta criada com sucesso!');
  loginSuccess(data.usuario);
}

function loginSuccess(user) {
  currentUser = user;
  localStorage.setItem('tasko_user', JSON.stringify(user));
  setupUserUI();
  navigate('home');
  updateActivitiesBadge();
}

function logout() {
  currentUser = null;
  localStorage.removeItem('tasko_user');
  document.getElementById('bottom-nav').classList.remove('visible');
  document.querySelectorAll('.screen').forEach(s => s.classList.remove('active'));
  document.getElementById('screen-auth').classList.add('active');
  document.getElementById('form-login').reset();
  document.getElementById('form-register').reset();
  showToast('Você saiu da conta.');
}

function canPublish() {
  return currentUser && (currentUser.tipo_perfil === 'PRESTADOR' || currentUser.tipo_perfil === 'AMBOS');
}

function setupUserUI() {
  if (!currentUser) return;
  document.getElementById('bottom-nav').classList.add('visible');

  const iniciais = currentUser.nome.split(' ').map(p => p[0]).slice(0, 2).join('').toUpperCase();
  document.getElementById('home-user-greeting').textContent = `${currentUser.nome} (${currentUser.tipo_perfil})`;
  document.getElementById('home-avatar').textContent = iniciais;

  document.getElementById('nav-publish').style.display = canPublish() ? 'flex' : 'none';
  document.getElementById('mural-add-btn').style.display = canPublish() ? 'inline-block' : 'none';

  if (canPublish()) {
    document.getElementById('cta-title').textContent = 'Pronto para anunciar?';
    document.getElementById('cta-desc').textContent = 'Publique seu serviço e apareça no mural agora';
    document.getElementById('cta-btn').textContent = '+ Publicar';
    document.getElementById('pub-regiao').value = currentUser.regiao || 'Zona Sul, SP';
  } else {
    document.getElementById('cta-title').textContent = 'Precisa de um profissional?';
    document.getElementById('cta-desc').textContent = 'Compare avaliações e solicite orçamento rápido';
    document.getElementById('cta-btn').textContent = 'Ver Mural';
  }

  renderCategories();
}

// ==========================================
// NAVEGAÇÃO E TELAS
// ==========================================
function navigate(screenName) {
  document.querySelectorAll('.screen').forEach(s => s.classList.remove('active'));
  document.getElementById(`screen-${screenName}`).classList.add('active');

  document.querySelectorAll('.nav-item').forEach(item => {
    item.classList.toggle('active', item.dataset.target === screenName);
  });

  if (screenName === 'mural') loadServicos();
  if (screenName === 'activities') loadActivities();
  if (screenName === 'profile') renderProfile();
}

function handleBannerClick() {
  if (canPublish()) navigate('publish');
  else openMural('Todos');
}

function renderCategories() {
  const grid = document.getElementById('categories-grid');
  grid.innerHTML = CATEGORIES.map(cat => `
    <div class="cat-card ${cat.full ? 'full' : ''}" style="background:${cat.grad}" onclick="openMural('${cat.name}')">
      <img src="${cat.img}" alt="${cat.name}" onerror="this.style.display='none'" />
      <div class="cat-card-content">
        <div class="cat-icon">${cat.icon}</div>
        <div class="cat-name">${cat.name}</div>
        <div class="cat-sub">${cat.sub}</div>
      </div>
    </div>
  `).join('');
}

function searchFromHome() {
  const q = document.getElementById('home-search-input').value;
  document.getElementById('mural-search').value = q;
  openMural('Todos');
}

function openMural(categoria) {
  activeCategory = categoria;
  renderChips();
  navigate('mural');
}

function renderChips() {
  const all = ['Todos', ...CATEGORIES.map(c => c.name)];
  document.getElementById('mural-chips').innerHTML = all.map(c => `
    <button class="chip ${activeCategory === c ? 'active' : ''}" onclick="selectChip('${c}')">${c}</button>
  `).join('');
}

function selectChip(cat) {
  activeCategory = cat;
  renderChips();
  loadServicos();
}

// ==========================================
// MURAL E DETALHES DE SERVIÇOS
// ==========================================
async function loadServicos() {
  const q = document.getElementById('mural-search').value || '';
  const params = new URLSearchParams({ categoria: activeCategory, q });
  const res = await fetch(`/api/servicos?${params.toString()}`);
  currentServices = await res.json();

  const list = document.getElementById('mural-list');
  if (!currentServices.length) {
    list.innerHTML = `<div class="empty-state">Nenhum anúncio encontrado para esse filtro.</div>`;
    return;
  }

  list.innerHTML = currentServices.map(s => {
    const meta = getCatMeta(s.categoria);
    const isMine = currentUser && s.prestador_id === currentUser.id;
    return `
      <div class="ad-card" onclick="openDetail(${s.id})">
        <div class="ad-thumb" style="background:${meta.grad}">
          <span>${meta.icon}</span>
          <img src="${meta.img}" alt="${esc(s.categoria)}" onerror="this.style.display='none'" />
        </div>
        <div class="ad-info">
          <div class="ad-top">
            <span class="badge">${esc(s.categoria)} ${isMine ? '• SEU' : ''}</span>
            <span class="rating">★ ${s.nota_media.toFixed(1)}</span>
          </div>
          <div class="ad-title">${esc(s.titulo)}</div>
          <div class="ad-provider">${esc(s.prestador_nome)} • ${esc(s.regiao)}</div>
          <div class="ad-footer">
            <span class="ad-price">${s.preco_base > 0 ? 'A partir de R$ ' + s.preco_base : 'Orçamento Grátis'}</span>
            <span style="font-size:11px; font-weight:800; color:var(--emerald-main);">Ver detalhes →</span>
          </div>
        </div>
      </div>
    `;
  }).join('');
}

function openDetail(serviceId) {
  const s = currentServices.find(item => item.id === serviceId);
  if (!s) return;
  selectedService = s;

  const meta = getCatMeta(s.categoria);
  const isMine = currentUser && s.prestador_id === currentUser.id;

  document.getElementById('detail-content').innerHTML = `
    <div class="detail-cover" style="background:${meta.grad}">
      <button class="back-btn" onclick="navigate('mural')">←</button>
      <span>${meta.icon}</span>
      <img src="${meta.img}" alt="${esc(s.categoria)}" onerror="this.style.display='none'" />
    </div>
    <div class="detail-body">
      <div class="ad-top" style="margin-bottom:8px;">
        <span class="badge">${esc(s.categoria)}</span>
        <span class="rating">★ ${s.nota_media.toFixed(1)} (${s.total_avaliacoes} avaliações)</span>
      </div>
      <h2 style="font-size:20px; color:var(--deep-pine); margin-bottom:4px;">${esc(s.titulo)}</h2>
      <p style="font-size:13px; color:var(--muted);">Prestador: <strong>${esc(s.prestador_nome)}</strong> • ${esc(s.regiao)}</p>

      <div class="metrics-row">
        <div class="metric-box">
          <div class="metric-val" style="color:var(--pure-emerald);">${s.preco_base > 0 ? 'R$ ' + s.preco_base : 'Grátis'}</div>
          <div class="metric-lbl">Base / Visita</div>
        </div>
        <div class="metric-box">
          <div class="metric-val">Verificado</div>
          <div class="metric-lbl">Perfil TASKO</div>
        </div>
        <div class="metric-box">
          <div class="metric-val">~20 min</div>
          <div class="metric-lbl">Resposta</div>
        </div>
      </div>

      <h4 style="font-size:14px; color:var(--deep-pine); margin-bottom:6px;">Sobre o Serviço</h4>
      <p style="font-size:13px; color:#475569; line-height:1.5; margin-bottom:14px;">${esc(s.descricao)}</p>

      <h4 style="font-size:14px; color:var(--deep-pine);">Serviços Inclusos</h4>
      <div class="tag-list">
        ${(s.servicos_inclusos || []).map(item => `<span class="tag-item">✓ ${esc(item)}</span>`).join('')}
      </div>

      ${isMine ? `
        <div style="background:var(--gold-soft); padding:10px; border-radius:10px; font-size:12px; color:#92400E; margin-bottom:12px; font-weight:700;">
          Este anúncio pertence à sua conta atual.
        </div>
        <button class="btn btn-danger" onclick="deleteService(${s.id})">Excluir Meu Anúncio</button>
      ` : `
        <button class="btn btn-primary" onclick="openHireModal()">Contratar / Solicitar Serviço</button>
      `}
    </div>
  `;

  navigate('detail');
}

// ==========================================
// PUBLICAR & EXCLUIR SERVIÇO
// ==========================================
async function handlePublishService(e) {
  e.preventDefault();
  const inclusosRaw = document.getElementById('pub-inclusos').value;
  const servicos_inclusos = inclusosRaw
    .split(',')
    .map(i => i.trim())
    .filter(Boolean);

  const payload = {
    prestador_id: currentUser.id,
    categoria: document.getElementById('pub-categoria').value,
    titulo: document.getElementById('pub-titulo').value,
    preco_base: document.getElementById('pub-preco').value,
    regiao: document.getElementById('pub-regiao').value,
    descricao: document.getElementById('pub-descricao').value,
    servicos_inclusos
  };

  const res = await fetch('/api/servicos', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  const data = await res.json();
  if (!res.ok) return showToast(data.erro || 'Erro ao publicar.');

  document.getElementById('form-publish').reset();
  document.getElementById('pub-regiao').value = currentUser.regiao || 'Zona Sul, SP';
  showToast('Serviço publicado no mural!');
  openMural('Todos');
}

async function deleteService(id) {
  if (!confirm('Deseja excluir este anúncio?')) return;
  const res = await fetch(`/api/servicos/${id}`, {
    method: 'DELETE',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ prestador_id: currentUser.id })
  });
  if (res.ok) {
    showToast('Anúncio excluído.');
    openMural('Todos');
  }
}

// ==========================================
// CONTRATAÇÕES, STATUS E AVALIAÇÕES
// ==========================================
function openHireModal() {
  document.getElementById('hire-service-name').textContent =
    `${selectedService.titulo} — com ${selectedService.prestador_nome}`;
  document.getElementById('modal-hire').classList.add('open');
}

function closeModal(id) {
  document.getElementById(id).classList.remove('open');
}

async function submitContratacao(e) {
  e.preventDefault();
  const payload = {
    servico_id: selectedService.id,
    cliente_id: currentUser.id,
    mensagem: document.getElementById('hire-msg').value,
    data_desejada: document.getElementById('hire-date').value
  };

  const res = await fetch('/api/contratacoes', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  const data = await res.json();
  if (!res.ok) return showToast(data.erro || 'Erro ao solicitar.');

  closeModal('modal-hire');
  document.getElementById('hire-msg').value = '';
  document.getElementById('hire-date').value = '';
  showToast('Solicitação enviada ao prestador!');
  updateActivitiesBadge();
  navigate('activities');
}

async function loadActivities() {
  const res = await fetch(`/api/contratacoes/${currentUser.id}`);
  const pedidos = await res.json();
  updateBadgeCount(pedidos);

  const container = document.getElementById('activities-list');
  if (!pedidos.length) {
    container.innerHTML = `<div class="empty-state">Nenhuma solicitação ainda.<br/>Quando você contratar ou receber pedidos, eles aparecerão aqui.</div>`;
    return;
  }

  container.innerHTML = pedidos.map(p => {
    const statusClass =
      p.status === 'Em Negociação' ? 'badge-gold' :
      p.status === 'Recusado' ? 'badge-red' : 'badge';

    const outroNome = p.papel_usuario === 'PRESTADOR'
      ? `Cliente: ${p.cliente_nome}`
      : `Prestador: ${p.prestador_nome}`;

    const contatoLiberado = p.papel_usuario === 'PRESTADOR' ? p.cliente_telefone : p.prestador_telefone;

    return `
      <div class="order-card">
        <div class="ad-top">
          <span class="badge">${p.papel_usuario === 'PRESTADOR' ? '📥 RECEBIDO' : '📤 ENVIADO'} • ${esc(p.servico_categoria)}</span>
          <span class="badge ${statusClass}">${esc(p.status)}</span>
        </div>
        <div style="font-size:14px; font-weight:800; color:var(--deep-pine); margin-top:6px;">${esc(p.servico_titulo)}</div>
        <div style="font-size:12px; color:var(--muted); margin-top:2px;">${esc(outroNome)} • Quando: <strong>${esc(p.data_desejada)}</strong></div>
        <div class="order-msg">"${esc(p.mensagem)}"</div>

        ${contatoLiberado ? `
          <div class="contact-unlocked">
            📞 Contato direto liberado: ${esc(contatoLiberado)}
          </div>
        ` : ''}

        ${p.avaliacao ? `
          <div style="margin-top:8px; font-size:12px; color:#92400E; background:var(--gold-soft); padding:8px; border-radius:8px;">
            <strong>Avaliação: ${'★'.repeat(p.avaliacao.nota)}</strong> — "${esc(p.avaliacao.comentario)}"
          </div>
        ` : ''}

        <div class="order-actions">
          ${p.papel_usuario === 'PRESTADOR' && p.status === 'Em Negociação' ? `
            <button class="btn btn-primary btn-sm" onclick="updateStatus(${p.id}, 'Combinado')">✓ Aceitar Serviço</button>
            <button class="btn btn-danger btn-sm" onclick="updateStatus(${p.id}, 'Recusado')">✕ Recusar</button>
          ` : ''}

          ${p.status === 'Combinado' ? `
            <button class="btn btn-dark btn-sm" onclick="updateStatus(${p.id}, 'Concluído')">🏁 Marcar como Concluído</button>
          ` : ''}

          ${p.papel_usuario === 'CLIENTE' && p.status === 'Concluído' && !p.avaliacao ? `
            <button class="btn btn-primary btn-sm" onclick="openRateModal(${p.id})">★ Avaliar Prestador</button>
          ` : ''}
        </div>
      </div>
    `;
  }).join('');
}

async function updateStatus(id, status) {
  const res = await fetch(`/api/contratacoes/${id}/status`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ status, usuario_id: currentUser.id })
  });
  if (res.ok) {
    showToast(`Status alterado para: ${status}`);
    loadActivities();
  }
}

function openRateModal(orderId) {
  ratingOrderId = orderId;
  document.getElementById('modal-rate').classList.add('open');
}

async function submitAvaliacao(e) {
  e.preventDefault();
  const payload = {
    nota: document.getElementById('rate-nota').value,
    comentario: document.getElementById('rate-comentario').value,
    cliente_id: currentUser.id
  };
  const res = await fetch(`/api/contratacoes/${ratingOrderId}/avaliar`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  if (res.ok) {
    closeModal('modal-rate');
    document.getElementById('rate-comentario').value = '';
    showToast('Avaliação registrada!');
    loadActivities();
  }
}

async function updateActivitiesBadge() {
  if (!currentUser) return;
  const res = await fetch(`/api/contratacoes/${currentUser.id}`);
  const pedidos = await res.json();
  updateBadgeCount(pedidos);
}

function updateBadgeCount(pedidos) {
  const pendentes = pedidos.filter(p => p.status === 'Em Negociação' || p.status === 'Combinado').length;
  const badge = document.getElementById('activities-badge');
  badge.textContent = pendentes;
  badge.style.display = pendentes > 0 ? 'flex' : 'none';
}

// ==========================================
// PERFIL
// ==========================================
function renderProfile() {
  document.getElementById('profile-content').innerHTML = `
    <div style="background:#fff; border:1.5px solid var(--border); border-radius:18px; padding:18px; margin-bottom:16px;">
      <span class="badge">${esc(currentUser.tipo_perfil)}</span>
      <h3 style="margin-top:8px; color:var(--deep-pine);">${esc(currentUser.nome)}</h3>
      <p style="font-size:13px; color:var(--muted); margin-top:4px;">📧 ${esc(currentUser.email)}</p>
      <p style="font-size:13px; color:var(--muted); margin-top:4px;">📞 ${esc(currentUser.telefone || 'Não informado')}</p>
      <p style="font-size:13px; color:var(--muted); margin-top:4px;">📍 ${esc(currentUser.regiao)}</p>
    </div>

    <div style="background:var(--emerald-soft); border:1px solid var(--emerald-border); border-radius:14px; padding:14px; font-size:12px; color:var(--emerald-main); margin-bottom:16px;">
      <strong>Dica para demonstração:</strong> clique em <em>Sair da Conta</em> abaixo para alternar entre o perfil de <strong>Prestador</strong> e o perfil de <strong>Cliente</strong> ao vivo!
    </div>

    <button class="btn btn-danger" onclick="logout()">Sair da Conta (Trocar Usuário)</button>
  `;
}

// Inicialização automática se já estiver logado
if (currentUser) {
  setupUserUI();
  navigate('home');
  updateActivitiesBadge();
}