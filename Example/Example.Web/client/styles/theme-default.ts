import { blue, pink, grey } from '@material-ui/core/colors';
import { createMuiTheme, ThemeOptions } from '@material-ui/core/styles';

export const defaultTheme = createMuiTheme({
	palette: {
		primary: blue,
		secondary: pink
	},
	appBar: {
		height: 57,
		color: blue[600]
	},
	drawer: {
		width: 230,
		color: grey[900]
	},
	raisedButton: {
		primaryColor: blue[600]
	}
} as ThemeOptions);

export default defaultTheme;
