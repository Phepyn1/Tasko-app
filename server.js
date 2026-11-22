const express = require('express');
const bcrypt = require('bcryptjs');
const fs = require('fs');
const path = require('path');

const app = express();
const PORT = 3000;
const DB_PATH = path.join(__dirname, 'db.json');

app.use(express.json());
app.use(express.static(path.join(__dirname, 'public')));

// Banco de dados inicial (criado automaticamente se não existir)
function initDB() {
  if (fs.existsSync(DB_PATH)) return;

  const senhaPadrao = bcrypt.hashSync('123456', 10);
  const initialData = {
    usuarios: [
      { id: 1, nome: 'Carlos Mendes', email: 'carlos@tasko.com', senha_hash: senhaPadrao, telefone: '11999990001', regiao: 'Zona Sul, SP', tipo_perfil: 'PRESTADOR', criado_em: new Date().toISOString() },
      { id: 2, nome: 'Roberto Almeida', email: 'roberto@tasko.com', senha_hash: senhaPadrao, telefone: '11999990002', regiao: 'Santo Amaro, SP', tipo_perfil: 'PRESTADOR', criado_em: new Date().toISOString() },
      { id: 3, nome: 'Valdir Construções', email: 'valdir@tasko.com', senha_hash: senhaPadrao, telefone: '11999990003', regiao: 'Interlagos, SP', tipo_perfil: 'PRESTADOR', criado_em: new Date().toISOString() },
      { id: 4, nome: 'Seu Jorge Madeira', email: 'jorge@tasko.com', senha_hash: senhaPadrao, telefone: '11999990004', regiao: 'Campo Limpo, SP', tipo_perfil: 'PRESTADOR', criado_em: new Date().toISOString() },
      { id: 5, nome: 'LimpaFácil Estofados', email: 'limpafacil@tasko.com', senha_hash: senhaPadrao, telefone: '11999990005', regiao: 'Zona Sul, SP', tipo_perfil: 'PRESTADOR', criado_em: new Date().toISOString() }
    ],
    servicos: [
      {
        id: 1, prestador_id: 1, categoria: 'Encanador',
        titulo: 'Caça-Vazamentos e Reparos Hidráulicos 24h',
        descricao: 'Especialista em detecção de vazamentos ocultos, reparo de tubulações de água fria/quente, limpeza de caixa d’água e desentupimentos com garantia.',
        preco_base: 80, regiao: 'Zona Sul, SP',
        servicos_inclusos: ['Caça-vazamentos', 'Desentupimento', 'Troca de registros', 'Caixa d’água'],
        nota_media: 4.9, total_avaliacoes: 12, criado_em: new Date().toISOString()
      },
      {
        id: 2, prestador_id: 2, categoria: 'Eletricista',
        titulo: 'Instalações Elétricas, Quadros e Chuveiros',
        descricao: 'Manutenção residencial e comercial seguindo normas de segurança. Troca de disjuntores, instalação de tomadas, luminárias e revisão de fiação antiga.',
        preco_base: 90, regiao: 'Santo Amaro, SP',
        servicos_inclusos: ['Quadro de força', 'Chuveiros', 'Tomadas e interruptores', 'Iluminação LED'],
        nota_media: 5.0, total_avaliacoes: 19, criado_em: new Date().toISOString()
      },
      {
        id: 3, prestador_id: 3, categoria: 'Mestre de Obras',
        titulo: 'Reformas Residenciais, Alvenaria e Pisos',
        descricao: 'Execução completa de reformas em casas e apartamentos. Assentamento de porcelanato, reboco, construção de muros e acabamento fino.',
        preco_base: 150, regiao: 'Interlagos, SP',
        servicos_inclusos: ['Alvenaria', 'Porcelanato e pisos', 'Reformas gerais', 'Orçamento no local'],
        nota_media: 4.8, total_avaliacoes: 9, criado_em: new Date().toISOString()
      },
      {
        id: 4, prestador_id: 4, categoria: 'Carpinteiro',
        titulo: 'Portas, Telhados, Decks e Pergolados',
        descricao: 'Mais de 15 anos trabalhando com madeira maciça. Instalação e ajuste de portas, fabricação de pergolados sob medida e restauração de decks.',
        preco_base: 120, regiao: 'Campo Limpo, SP',
        servicos_inclusos: ['Instalação de portas', 'Pergolados', 'Decks de madeira', 'Telhados'],
        nota_media: 4.9, total_avaliacoes: 15, criado_em: new Date().toISOString()
      },
      {
        id: 5, prestador_id: 5, categoria: 'Lavador de Sofá',
        titulo: 'Higienização Profunda e Impermeabilização',
        descricao: 'Limpeza extratora a seco para sofás, colchões e poltronas. Remoção de manchas, odores e tratamento antiácaro direto na sua casa.',
        preco_base: 140, regiao: 'Zona Sul, SP',
        servicos_inclusos: ['Lavagem a seco', 'Antiácaro', 'Colchões e poltronas', 'Impermeabilização'],
        nota_media: 5.0, total_avaliacoes: 22, criado_em: new Date().toISOString()
      }
    ],
    contratacoes: []
  };

  fs.writeFileSync(DB_PATH, JSON.stringify(initialData, null, 2));
}

function readDB() {
  initDB();
  return JSON.parse(fs.readFileSync(DB_PATH, 'utf-8'));
}

function writeDB(data) {
  fs.writeFileSync(DB_PATH, JSON.stringify(data, null, 2));
}

// ==========================================
// ROTAS DE AUTENTICAÇÃO (RF01, RF02, RF03)
// ==========================================
app.post('/api/register', (req, res) => {
  const { nome, email, senha, telefone, regiao, tipo_perfil } = req.body;
  if (!nome || !email || !senha || !tipo_perfil) {
    return res.status(400).json({ erro: 'Preencha todos os campos obrigatórios.' });
  }

  const db = readDB();
  const emailNormalizado = email.trim().toLowerCase();
  if (db.usuarios.some(u => u.email === emailNormalizado)) {
    return res.status(409).json({ erro: 'Este e-mail já está cadastrado.' });
  }

  const novoUsuario = {
    id: db.usuarios.length ? Math.max(...db.usuarios.map(u => u.id)) + 1 : 1,
    nome: nome.trim(),
    email: emailNormalizado,
    senha_hash: bcrypt.hashSync(senha, 10),
    telefone: (telefone || '').trim(),
    regiao: (regiao || 'São Paulo, SP').trim(),
    tipo_perfil, // CLIENTE | PRESTADOR | AMBOS
    criado_em: new Date().toISOString()
  };

  db.usuarios.push(novoUsuario);
  writeDB(db);

  const { senha_hash, ...usuarioSeguro } = novoUsuario;
  res.status(201).json({ usuario: usuarioSeguro });
});

app.post('/api/login', (req, res) => {
  const { email, senha } = req.body;
  if (!email || !senha) {
    return res.status(400).json({ erro: 'Informe e-mail e senha.' });
  }

  const db = readDB();
  const usuario = db.usuarios.find(u => u.email === email.trim().toLowerCase());
  if (!usuario || !bcrypt.compareSync(senha, usuario.senha_hash)) {
    return res.status(401).json({ erro: 'E-mail ou senha inválidos.' });
  }

  const { senha_hash, ...usuarioSeguro } = usuario;
  res.json({ usuario: usuarioSeguro });
});

// ==========================================
// ROTAS DE SERVIÇOS / ANÚNCIOS (RF05, RF07, RF08, RF19)
// ==========================================
app.get('/api/servicos', (req, res) => {
  const { categoria, q } = req.query;
  const db = readDB();

  let lista = db.servicos.map(s => {
    const prestador = db.usuarios.find(u => u.id === s.prestador_id);
    return {
      ...s,
      prestador_nome: prestador ? prestador.nome : 'Profissional',
      prestador_telefone: prestador ? prestador.telefone : ''
    };
  });

  if (categoria && categoria !== 'Todos') {
    lista = lista.filter(s => s.categoria === categoria);
  }

  if (q && q.trim()) {
    const termo = q.trim().toLowerCase();
    lista = lista.filter(s =>
      s.titulo.toLowerCase().includes(termo) ||
      s.descricao.toLowerCase().includes(termo) ||
      s.categoria.toLowerCase().includes(termo) ||
      s.prestador_nome.toLowerCase().includes(termo) ||
      s.regiao.toLowerCase().includes(termo)
    );
  }

  // Mais recentes primeiro (para seu anúncio criado em sala aparecer no topo!)
  lista.sort((a, b) => b.id - a.id);
  res.json(lista);
});

app.post('/api/servicos', (req, res) => {
  const { prestador_id, categoria, titulo, descricao, preco_base, regiao, servicos_inclusos } = req.body;
  if (!prestador_id || !categoria || !titulo || !descricao) {
    return res.status(400).json({ erro: 'Preencha categoria, título e descrição do serviço.' });
  }

  const db = readDB();
  const prestador = db.usuarios.find(u => u.id === Number(prestador_id));
  if (!prestador || (prestador.tipo_perfil !== 'PRESTADOR' && prestador.tipo_perfil !== 'AMBOS')) {
    return res.status(403).json({ erro: 'Apenas contas de Prestador podem publicar serviços.' });
  }

  const novoServico = {
    id: db.servicos.length ? Math.max(...db.servicos.map(s => s.id)) + 1 : 1,
    prestador_id: Number(prestador_id),
    categoria,
    titulo: titulo.trim(),
    descricao: descricao.trim(),
    preco_base: Number(preco_base) || 0,
    regiao: (regiao || prestador.regiao || 'São Paulo, SP').trim(),
    servicos_inclusos: Array.isArray(servicos_inclusos) && servicos_inclusos.length
      ? servicos_inclusos
      : ['Atendimento agendado', 'Orçamento transparente'],
    nota_media: 5.0,
    total_avaliacoes: 0,
    criado_em: new Date().toISOString()
  };

  db.servicos.push(novoServico);
  writeDB(db);
  res.status(201).json(novoServico);
});

app.delete('/api/servicos/:id', (req, res) => {
  const id = Number(req.params.id);
  const { prestador_id } = req.body;
  const db = readDB();

  const index = db.servicos.findIndex(s => s.id === id && s.prestador_id === Number(prestador_id));
  if (index === -1) {
    return res.status(404).json({ erro: 'Anúncio não encontrado ou sem permissão.' });
  }

  db.servicos.splice(index, 1);
  writeDB(db);
  res.json({ ok: true });
});

// ==========================================
// ROTAS DE CONTRATAÇÃO / PROPOSTAS (RF10-RF15)
// ==========================================
app.post('/api/contratacoes', (req, res) => {
  const { servico_id, cliente_id, mensagem, data_desejada } = req.body;
  if (!servico_id || !cliente_id || !mensagem) {
    return res.status(400).json({ erro: 'Descreva o que você precisa para solicitar o serviço.' });
  }

  const db = readDB();
  const servico = db.servicos.find(s => s.id === Number(servico_id));
  if (!servico) {
    return res.status(404).json({ erro: 'Serviço não encontrado.' });
  }

  if (servico.prestador_id === Number(cliente_id)) {
    return res.status(400).json({ erro: 'Você não pode contratar o seu próprio serviço.' });
  }

  const novaContratacao = {
    id: db.contratacoes.length ? Math.max(...db.contratacoes.map(c => c.id)) + 1 : 1,
    servico_id: servico.id,
    cliente_id: Number(cliente_id),
    prestador_id: servico.prestador_id,
    mensagem: mensagem.trim(),
    data_desejada: data_desejada || 'A combinar',
    status: 'Em Negociação', // Em Negociação | Combinado | Concluído | Recusado
    avaliacao: null,
    criado_em: new Date().toISOString()
  };

  db.contratacoes.push(novaContratacao);
  writeDB(db);
  res.status(201).json(novaContratacao);
});

app.get('/api/contratacoes/:usuario_id', (req, res) => {
  const uid = Number(req.params.usuario_id);
  const db = readDB();

  const lista = db.contratacoes
    .filter(c => c.cliente_id === uid || c.prestador_id === uid)
    .map(c => {
      const servico = db.servicos.find(s => s.id === c.servico_id);
      const cliente = db.usuarios.find(u => u.id === c.cliente_id);
      const prestador = db.usuarios.find(u => u.id === c.prestador_id);

      return {
        ...c,
        papel_usuario: c.prestador_id === uid ? 'PRESTADOR' : 'CLIENTE',
        servico_titulo: servico ? servico.titulo : 'Serviço removido',
        servico_categoria: servico ? servico.categoria : 'Geral',
        servico_preco: servico ? servico.preco_base : 0,
        cliente_nome: cliente ? cliente.nome : 'Cliente',
        cliente_telefone: c.status === 'Combinado' || c.status === 'Concluído' ? (cliente?.telefone || '') : null,
        prestador_nome: prestador ? prestador.nome : 'Prestador',
        prestador_telefone: c.status === 'Combinado' || c.status === 'Concluído' ? (prestador?.telefone || '') : null
      };
    })
    .sort((a, b) => b.id - a.id);

  res.json(lista);
});

app.patch('/api/contratacoes/:id/status', (req, res) => {
  const id = Number(req.params.id);
  const { status, usuario_id } = req.body;
  const permitidos = ['Combinado', 'Recusado', 'Concluído'];

  if (!permitidos.includes(status)) {
    return res.status(400).json({ erro: 'Status inválido.' });
  }

  const db = readDB();
  const contratacao = db.contratacoes.find(c => c.id === id);
  if (!contratacao) {
    return res.status(404).json({ erro: 'Solicitação não encontrada.' });
  }

  if (contratacao.prestador_id !== Number(usuario_id) && contratacao.cliente_id !== Number(usuario_id)) {
    return res.status(403).json({ erro: 'Sem permissão para alterar este pedido.' });
  }

  contratacao.status = status;
  writeDB(db);
  res.json(contratacao);
});

app.post('/api/contratacoes/:id/avaliar', (req, res) => {
  const id = Number(req.params.id);
  const { nota, comentario, cliente_id } = req.body;
  const db = readDB();

  const contratacao = db.contratacoes.find(c => c.id === id && c.cliente_id === Number(cliente_id));
  if (!contratacao) return res.status(404).json({ erro: 'Contratação não encontrada.' });
  if (contratacao.status !== 'Concluído') return res.status(400).json({ erro: 'Só é possível avaliar serviços concluídos.' });

  contratacao.avaliacao = {
    nota: Math.min(5, Math.max(1, Number(nota) || 5)),
    comentario: (comentario || '').trim(),
    data: new Date().toISOString()
  };

  // Atualiza média do serviço
  const servico = db.servicos.find(s => s.id === contratacao.servico_id);
  if (servico) {
    const somaAtual = servico.nota_media * servico.total_avaliacoes;
    servico.total_avaliacoes += 1;
    servico.nota_media = Number(((somaAtual + contratacao.avaliacao.nota) / servico.total_avaliacoes).toFixed(1));
  }

  writeDB(db);
  res.json(contratacao);
});

initDB();
app.listen(PORT, () => {
  console.log(`\n💎 TASKO rodando em: http://localhost:${PORT}`);
  console.log(`📁 Banco de dados salvo em: ${DB_PATH}\n`);
});