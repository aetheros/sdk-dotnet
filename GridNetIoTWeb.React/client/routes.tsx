import * as React from 'react';
import ReactDOM from 'react-dom';
import App from './views/App';
import Dashboard from './views/Dashboard';
import FormPage from './views/FormPage';
import MeterDashboard from './views/MeterDashboard';
import MeterList from './views/MeterList';
import TablePage from './views/TablePage';


// Import all the routeable views into the global window variable.
Object.assign(window, {
	Dashboard,
	MeterList,
	MeterDashboard,

	FormPage,
	TablePage,
});

// Hot module replacement.  
if (module.hot) {
	const render = (react, elemId) => {
		ReactDOM.unmountComponentAtNode(document.getElementById(elemId));
		ReactDOM.render(React.createElement(react), document.getElementById(elemId));
	}

	module.hot.accept('./views/App.tsx', () => render(require('./views/App').default, 'App'));
	module.hot.accept('./views/MeterList.tsx', () => render(require('./views/MeterList').default, 'Content'));
	module.hot.accept('./views/MeterDashboard.tsx', () => render(require('./views/MeterDashboard').default, 'Content'));

	module.hot.accept('./views/Dashboard.js', () => render(require('./views/Dashboard').default, 'Content'));
	module.hot.accept('./views/FormPage.js', () => render(require('./views/FormPage').default, 'Content'));
	module.hot.accept('./views/TablePage.js', () => render(require('./views/TablePage').default, 'Content'));
}

export default App;