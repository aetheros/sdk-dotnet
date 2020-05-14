import Snackbar from "@material-ui/core/Snackbar";
//import MuiThemeProvider from "@material-ui/core/styles/MuiThemeProvider";
import { ThemeProvider } from '@material-ui/core/styles';
import dotnetify, { dotnetifyVM } from "dotnetify";
import * as React from "react";
import BasePage from "../components/BasePage";
import MeterListTable from "../components/meter/MeterListTable";
import defaultTheme from '../styles/theme-default';

type Props = {
};

export default class MeterList extends React.Component<Props> {
	state = {
		ShowNotification: false,
		Meters: [],
	};
	constructor(props) {
		super(props);
		this.vm = dotnetify.react.connect("MeterList", this);
	}

	vm: dotnetifyVM;
	dispatch(state: any) { return dotnetifyVM; }
	componentWillUnmount() { this.vm.$destroy(); }

	render() {
		//			<MuiThemeProvider muiTheme={ThemeDefault}>
		//			</MuiThemeProvider>

		return (
			<ThemeProvider theme={defaultTheme}>
				<BasePage title="Meter List" navigation="">
					<div>
						<MeterListTable data={this.state.Meters} />
						<Snackbar open={this.state.ShowNotification} message="Changes saved" autoHideDuration={1000} onClose={() => this.setState({ ShowNotification: false })} />
					</div>
				</BasePage>
			</ThemeProvider>
		);
	}
}
